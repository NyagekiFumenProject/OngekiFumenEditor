#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Logging;

/// <summary>
/// Browser-owned file log sink: one timestamped session file per run under
/// <c>opfs:/logs</c>, written through the origin-rooted OPFS interop module.
/// </summary>
[SupportedOSPlatform("browser")]
[RegisterSingleton<ILogOutput>]
public sealed class BrowserFileLogOutput : ILogOutput, IFileLogOutput
{
    public const string LogDirectoryPathValue = "opfs:/logs";

    private static readonly SemaphoreSlim MutationGate = new(1, 1);
    private static int nextWriteBufferHandle;

    private readonly Lazy<Task<string?>> file;
    private readonly object sync = new();
    private Task pendingWrite = Task.CompletedTask;

    public BrowserFileLogOutput()
    {
        bool available;
        try
        {
            available = BrowserLogFileSystemInterop.IsAvailable();
        }
        catch
        {
            available = false;
        }
        IsAvailable = available;

        file = new Lazy<Task<string?>>(
            CreateCurrentFileAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string LogDirectoryPath => LogDirectoryPathValue;

    private bool IsAvailable { get; }

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
        var currentFileName = file.Value.GetAwaiter().GetResult();
        return currentFileName is null ? string.Empty : $"{LogDirectoryPathValue}/{currentFileName}";
    }

    private async Task<string?> CreateCurrentFileAsync()
    {
        if (!IsAvailable)
            return null;

        try
        {
            string prefix = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
            string? fileName = await TryCreateUniqueFileAsync(prefix, ".log", CancellationToken.None);
            if (fileName is null)
                return null;

            await AppendCoreAsync(
                fileName,
                Encoding.UTF8.GetBytes(IFileLogOutput.BeginFileLogOutputMarker),
                CancellationToken.None);
            return fileName;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to initialize browser file log output: {exception}");
            return null;
        }
    }

    private async Task AppendAfterAsync(Task previousWrite, string content)
    {
        await previousWrite;
        var currentFileName = await file.Value;
        if (currentFileName is null)
            return;

        try
        {
            await AppendCoreAsync(currentFileName, Encoding.UTF8.GetBytes(content), CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Match the original WPF sink: logging failures are diagnostic-only and must not crash the app.
            Debug.WriteLine($"Failed to append browser file log output: {exception}");
        }
    }

    private static async Task<string?> TryCreateUniqueFileAsync(
        string prefix,
        string extension,
        CancellationToken cancellationToken)
    {
        ValidateFileNamePart(prefix, nameof(prefix));
        ValidateFileNamePart(extension, nameof(extension));

        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            for (int suffix = 0; ; suffix++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = suffix == 0
                    ? $"{prefix}{extension}"
                    : $"{prefix}_{suffix}{extension}";
                if (await BrowserLogFileSystemInterop.TryCreateFileAsync(fileName))
                    return fileName;
            }
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static async Task AppendCoreAsync(
        string fileName,
        byte[] data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            int handle = Interlocked.Increment(ref nextWriteBufferHandle);
            try
            {
                BrowserLogFileSystemInterop.SetWriteBuffer(handle, data, data.Length);
                await BrowserLogFileSystemInterop.AppendFileAsync(fileName, handle);
            }
            finally
            {
                BrowserLogFileSystemInterop.ReleaseWriteBuffer(handle);
            }
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static void ValidateFileNamePart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains('/') || value.Contains('\\') || value is "." or "..")
            throw new ArgumentException("Log file name parts cannot contain path separators.", parameterName);
    }
}
