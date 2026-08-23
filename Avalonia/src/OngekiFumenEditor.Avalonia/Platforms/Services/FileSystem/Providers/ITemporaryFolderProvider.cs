#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public interface ITemporaryFolderProvider
{
    bool IsAvailable { get; }

    ISimpleDirectory Root { get; }

    Task<ISimpleFile> CreateUniqueFileAsync(
        string prefix = "tempFile",
        string extension = ".dat",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default);

    Task<ISimpleDirectory> CreateUniqueFolderAsync(
        string prefix = "tempFolder",
        ISimpleDirectory? parent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every entry below <see cref="Root"/> while keeping the provider usable.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
