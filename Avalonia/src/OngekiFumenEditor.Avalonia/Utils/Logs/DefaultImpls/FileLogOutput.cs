#nullable enable

using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

internal static class FileLogOutput
{
    private static FileLogOutputWrapper? current;

    public static void WaitForWriteDone()
    {
        current?.FlushAsync().GetAwaiter().GetResult();
    }

    public static Task WriteLog(string content) =>
        current?.WriteLogAsync(content) ?? Task.CompletedTask;

    public static string GetCurrentLogFile() =>
        current?.GetCurrentLogFile() ?? string.Empty;

    internal static void SetCurrent(FileLogOutputWrapper output)
    {
        current = output;
    }
}

[RegisterSingleton<ILogOutput>]
public sealed class FileLogOutputWrapper : ILogOutput
{
    internal const string BeginFileLogOutputMarker = "----------BEGIN FILE LOG OUTPUT----------\n";

    private readonly ILogFileStorage storage;
    private readonly Func<DateTime> getNow;
    private readonly Lazy<Task<ILogFile?>> file;
    private readonly object sync = new();
    private Task pendingWrite = Task.CompletedTask;

    public FileLogOutputWrapper(ILogFileStorage storage)
        : this(storage, static () => DateTime.Now)
    {
    }

    internal FileLogOutputWrapper(ILogFileStorage storage, Func<DateTime> getNow)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(getNow);
        this.storage = storage;
        this.getNow = getNow;
        file = new Lazy<Task<ILogFile?>>(
            CreateCurrentFileAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
        FileLogOutput.SetCurrent(this);
    }

    public void WriteLog(Severity severity, string content)
    {
        _ = WriteLogAsync(content);
    }

    internal Task WriteLogAsync(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        lock (sync)
        {
            pendingWrite = AppendAfterAsync(pendingWrite, content);
            return pendingWrite;
        }
    }

    internal Task FlushAsync()
    {
        lock (sync)
        {
            return pendingWrite;
        }
    }

    internal string GetCurrentLogFile()
    {
        var currentFile = file.Value.GetAwaiter().GetResult();
        return currentFile?.Path ?? string.Empty;
    }

    internal Task<ILogFile?> GetCurrentFileAsync() => file.Value;

    private async Task<ILogFile?> CreateCurrentFileAsync()
    {
        if (!storage.IsAvailable)
            return null;

        try
        {
            string prefix = getNow().ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
            var currentFile = await storage.CreateUniqueFileAsync(prefix, ".log").ConfigureAwait(false);
            if (currentFile is not null)
            {
                await currentFile.AppendAsync(Encoding.UTF8.GetBytes(BeginFileLogOutputMarker))
                    .ConfigureAwait(false);
            }
            return currentFile;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to initialize file log output: {exception}");
            return null;
        }
    }

    private async Task AppendAfterAsync(Task previousWrite, string content)
    {
        await previousWrite.ConfigureAwait(false);
        var currentFile = await file.Value.ConfigureAwait(false);
        if (currentFile is null)
            return;

        try
        {
            await currentFile.AppendAsync(Encoding.UTF8.GetBytes(content)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Match the original WPF sink: logging failures are diagnostic-only and must not crash the app.
            Debug.WriteLine($"Failed to append file log output: {exception}");
        }
    }
}
