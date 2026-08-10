#nullable enable

using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Logging;

[RegisterSingleton<ILogFileStorage>]
public sealed class DesktopLogFileStorage : ILogFileStorage
{
    public const string LogFolderName = "logs";
    private const int BufferSize = 81_920;
    private readonly string logDirectoryPath;

    public DesktopLogFileStorage()
        : this(AppContext.BaseDirectory)
    {
    }

    internal DesktopLogFileStorage(string executableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);
        logDirectoryPath = Path.GetFullPath(Path.Combine(executableDirectory, LogFolderName));
    }

    public static string DefaultLogDirectoryPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, LogFolderName));

    public bool IsAvailable => true;

    public string LogDirectoryPath => logDirectoryPath;

    public Task<ILogFile?> CreateUniqueFileAsync(
        string prefix,
        string extension = ".log",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateFileNamePart(prefix, nameof(prefix));
        ValidateFileNamePart(extension, nameof(extension));
        Directory.CreateDirectory(logDirectoryPath);

        for (int suffix = 0; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                return Task.FromResult<ILogFile?>(new DesktopLogFile(filePath));
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

    private sealed class DesktopLogFile(string filePath) : ILogFile
    {
        public string Path { get; } = System.IO.Path.GetFullPath(filePath);

        public async Task AppendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new FileStream(
                Path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
