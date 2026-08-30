#nullable enable

using System.Globalization;
using System.Diagnostics;
using System.Text;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using static OngekiFumenEditor.Avalonia.Utils.Logs.ILogOutput;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;

/// <summary>
/// Desktop-owned file log sink: one timestamped session file per run under the
/// executable-side <c>logs</c> folder, appended sequentially and flushed on demand.
/// </summary>
[RegisterSingleton<ILogOutput>]
public sealed class DesktopFileLogOutput : ILogOutput, IFileLogOutput
{
    public const string LogFolderName = "logs";
    private const int BufferSize = 81_920;

    private static DesktopFileLogOutput? current;

    private readonly string logDirectoryPath;
    private readonly Func<DateTime> getNow;
    private readonly Lazy<Task<string?>> file;
    private readonly object sync = new();
    private Task pendingWrite = Task.CompletedTask;

    public DesktopFileLogOutput()
        : this(AppContext.BaseDirectory)
    {
    }

    internal DesktopFileLogOutput(string executableDirectory, Func<DateTime>? getNow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);
        this.getNow = getNow ?? (static () => DateTime.Now);
        logDirectoryPath = Path.GetFullPath(Path.Combine(executableDirectory, LogFolderName));
        file = new Lazy<Task<string?>>(
            CreateCurrentFileAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
        current = this;
    }

    public string LogDirectoryPath => logDirectoryPath;

    public static string DefaultLogDirectoryPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, LogFolderName));

    /// <summary>Awaits until every queued record of the live session file has been written.</summary>
    public static void WaitForWriteDone()
    {
        current?.FlushAsync().GetAwaiter().GetResult();
    }

    /// <summary>Appends raw content to the live session file without severity decoration.</summary>
    public static Task WriteLog(string content) =>
        current?.WriteLogAsync(content) ?? Task.CompletedTask;

    public void WriteLog(Severity severity, string content)
    {
        _ = WriteLogAsync(content);
    }

    /// <summary>Returns the live session file path, or an empty string when unavailable.</summary>
    public static string CurrentLogFile =>
        current?.GetCurrentLogFile() ?? string.Empty;

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
        return currentFile ?? string.Empty;
    }

    private async Task<string?> CreateCurrentFileAsync()
    {
        try
        {
            Directory.CreateDirectory(logDirectoryPath);
            string prefix = getNow().ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
            string? filePath = TryCreateUniqueFile(prefix, ".log");
            if (filePath is null)
                return null;

            await File.AppendAllTextAsync(filePath, IFileLogOutput.BeginFileLogOutputMarker, Encoding.UTF8)
                ;
            return filePath;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to initialize file log output: {exception}");
            return null;
        }
    }

    private string? TryCreateUniqueFile(string prefix, string extension)
    {
        ValidateFileNamePart(prefix, nameof(prefix));
        ValidateFileNamePart(extension, nameof(extension));

        for (int suffix = 0; ; suffix++)
        {
            string fileName = suffix == 0
                ? $"{prefix}{extension}"
                : $"{prefix}_{suffix}{extension}";
            string filePath = Path.Combine(logDirectoryPath, fileName);

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    1,
                    FileOptions.Asynchronous);
                return filePath;
            }
            catch (IOException) when (File.Exists(filePath) || Directory.Exists(filePath))
            {
                // Preserve the original one-log-file-per-session behavior without overwriting an existing run.
            }
        }
    }

    private static void ValidateFileNamePart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Log file name parts cannot contain path separators or invalid characters.", parameterName);
        }
    }

    private async Task AppendAfterAsync(Task previousWrite, string content)
    {
        await previousWrite;
        var filePath = await file.Value;
        if (filePath is null)
            return;

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(content));
            await stream.FlushAsync();
        }
        catch (Exception exception)
        {
            // Match the original WPF sink: logging failures are diagnostic-only and must not crash the app.
            Debug.WriteLine($"Failed to append file log output: {exception}");
        }
    }
}
