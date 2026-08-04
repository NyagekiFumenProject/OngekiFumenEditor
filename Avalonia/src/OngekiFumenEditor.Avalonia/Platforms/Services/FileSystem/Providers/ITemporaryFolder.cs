#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public interface ITemporaryFolder : ITemporaryEntry
{
    Task<ITemporaryFile?> TryGetFileAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<ITemporaryFolder?> TryGetFolderAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<ITemporaryFile> GetOrCreateFileAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<ITemporaryFolder> GetOrCreateFolderAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes this folder and all descendants. Deleting a missing folder is a no-op.
    /// </summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
}
