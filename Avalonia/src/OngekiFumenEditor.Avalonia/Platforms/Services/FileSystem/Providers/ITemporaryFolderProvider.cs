#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public interface ITemporaryFolderProvider
{
    bool IsAvailable { get; }

    ITemporaryFolder Root { get; }

    Task<ITemporaryFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ITemporaryFolder? parent = null,
        CancellationToken cancellationToken = default);

    Task<ITemporaryFolder> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ITemporaryFolder? parent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry below <see cref="Root"/> while keeping the provider usable.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
