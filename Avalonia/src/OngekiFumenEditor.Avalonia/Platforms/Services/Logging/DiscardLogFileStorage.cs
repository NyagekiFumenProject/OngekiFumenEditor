#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.Logging;

/// <summary>
/// Explicitly represents a host without persistent log-file storage.
/// </summary>
public sealed class DiscardLogFileStorage : ILogFileStorage
{
    public bool IsAvailable => false;

    public string LogDirectoryPath => string.Empty;

    public Task<ILogFile?> CreateUniqueFileAsync(
        string prefix,
        string extension = ".log",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ILogFile?>(null);
    }
}
