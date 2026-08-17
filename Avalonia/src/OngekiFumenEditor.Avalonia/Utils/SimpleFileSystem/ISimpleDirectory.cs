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
    ///     The local file-system path when the backing provider exposes one.
    /// </summary>
    string? LocalPath { get; }

    /// <summary>
    ///     A directory name such as "MyFolderA".
    /// </summary>
    string DirectoryName { get; }

    bool ExistsDirectory(string dirName);

    bool ExistsFile(string fileName);

    ISimpleFile[] GetFiles(string pattern = "*");

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
}

public readonly record struct SimpleDirectoryEntry(string Name, bool IsDirectory);
