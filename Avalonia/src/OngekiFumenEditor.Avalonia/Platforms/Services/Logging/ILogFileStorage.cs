#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.Logging;

/// <summary>
/// Provides the platform-owned persistent folder used for application log files.
/// </summary>
public interface ILogFileStorage
{
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a user-facing description of the actual log folder.
    /// </summary>
    string LogDirectoryPath { get; }

    Task<ILogFile?> CreateUniqueFileAsync(
        string prefix,
        string extension = ".log",
        CancellationToken cancellationToken = default);
}

public interface ILogFile
{
    /// <summary>
    /// Gets the real local path or a platform-qualified path such as <c>opfs:/logs/file.log</c>.
    /// </summary>
    string Path { get; }

    Task AppendAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);
}
