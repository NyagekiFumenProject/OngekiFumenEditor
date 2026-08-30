#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public interface ISimpleDirectory : IDisposable
{
    ISimpleDirectory? ParentDictionary { get; }

    ISimpleDirectory[] ChildDictionaries { get; }

    ISimpleFile[] ChildFiles { get; }

    /// <summary>
    ///     The virtual path within the simple file system; this is not necessarily a local path.
    /// </summary>
    string FullPath { get; }

    /// <summary>
    ///     A directory name such as "MyFolderA".
    /// </summary>
    string DirectoryName { get; }

    bool ExistsDirectory(string dirName);

    bool ExistsFile(string fileName);

    ISimpleFile[] GetFiles(string pattern = "*");

    Task<ISimpleFile?> TryGetFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ChildFiles.FirstOrDefault(file =>
            file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)));
    }

    Task<ISimpleDirectory?> TryGetDirectoryAsync(
        string directoryName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ChildDictionaries.FirstOrDefault(directory =>
            directory.DirectoryName.Equals(directoryName, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns a point-in-time root entry snapshot without taking ownership of the entries.
    /// Storage-backed implementations use this to catch conflicts created after a picker
    /// returned its initial directory tree.
    /// </summary>
    async Task<IReadOnlyList<SimpleDirectoryEntry>> GetEntrySnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return [
            .. ChildDictionaries.Select(directory => new SimpleDirectoryEntry(directory.DirectoryName, true)),
            .. ChildFiles.Select(file => new SimpleDirectoryEntry(file.FileName, false))
        ];
    }

    Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
        string directoryName,
        CancellationToken cancellationToken = default);

    Task<ISimpleFile> CreateFileAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    async Task<ISimpleFile> GetOrCreateFileAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ISimpleFile? existing = await TryGetFileAsync(fileName, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return existing;
        if (await TryGetDirectoryAsync(fileName, cancellationToken).ConfigureAwait(false) is not null)
            throw new IOException($"A directory already exists at '{fileName}'.");
        return await CreateFileAsync(fileName, cancellationToken).ConfigureAwait(false);
    }

    Task DeleteAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"Deleting '{FullPath}' is not supported by this directory provider.");
}

public readonly record struct SimpleDirectoryEntry(string Name, bool IsDirectory);
