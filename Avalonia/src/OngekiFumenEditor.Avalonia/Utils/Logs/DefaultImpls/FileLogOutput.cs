using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using System.Text;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;

internal static class FileLogOutput
{
    private static FileLogOutputWrapper current;

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
    private readonly Lazy<Task<ITemporaryFile>> file;
    private readonly object sync = new();
    private Task pendingWrite = Task.CompletedTask;

    public FileLogOutputWrapper(ITemporaryFolderProvider temporaryFolderProvider)
    {
        ArgumentNullException.ThrowIfNull(temporaryFolderProvider);
        file = new Lazy<Task<ITemporaryFile>>(
            async () =>
            {
                var logs = await temporaryFolderProvider.Root.GetOrCreateFolderAsync("logs");
                var runtime = await logs.GetOrCreateFolderAsync("runtime");
                return await temporaryFolderProvider.CreateUniqueFileAsync(
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                    ".log",
                    runtime);
            },
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
        return currentFile.LocalPath ?? currentFile.RelativePath;
    }

    internal Task<ITemporaryFile> GetCurrentFileAsync() => file.Value;

    private async Task AppendAfterAsync(Task previousWrite, string content)
    {
        await previousWrite.ConfigureAwait(false);
        var currentFile = await file.Value.ConfigureAwait(false);
        await currentFile.AppendAsync(Encoding.UTF8.GetBytes(content)).ConfigureAwait(false);
    }
}
