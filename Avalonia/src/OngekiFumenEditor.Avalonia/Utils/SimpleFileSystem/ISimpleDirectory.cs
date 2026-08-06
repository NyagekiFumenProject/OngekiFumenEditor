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

    Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
        string directoryName,
        CancellationToken cancellationToken = default);

    Task<ISimpleFile> CreateFileAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}
