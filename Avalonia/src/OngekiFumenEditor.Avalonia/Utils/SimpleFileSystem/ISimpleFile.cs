#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public interface ISimpleFile : IDisposable
{
    ISimpleDirectory? ParentDictionary { get; }

    /// <summary>
    ///     The virtual path within the simple file system; this is not necessarily a local path.
    /// </summary>
    string FullPath { get; }

    /// <summary>
    ///     The local file-system path when the backing provider exposes one.
    /// </summary>
    string? LocalPath { get; }

    /// <summary>
    ///     A file name such as "myFile.txt".
    /// </summary>
    string FileName { get; }

    long FileLength { get; }

    ValueTask<string[]> ReadAllLines();

    ValueTask<byte[]> ReadAllBytes();

    Task<Stream> OpenRead();

    /// <summary>
    ///     Opens a stream that replaces the current content when the backing provider supports writing.
    /// </summary>
    Task<Stream> OpenWrite();

    Task DeleteAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"Deleting '{FullPath}' is not supported by this file provider.");

    /// <summary>
    ///     Replaces the file content after <paramref name="writer"/> completes successfully.
    ///     Local files are committed atomically through a temporary file in the same directory.
    /// </summary>
    Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default) =>
        SimpleFileWriteTransaction.WriteAsync(this, writer, cancellationToken);
}
