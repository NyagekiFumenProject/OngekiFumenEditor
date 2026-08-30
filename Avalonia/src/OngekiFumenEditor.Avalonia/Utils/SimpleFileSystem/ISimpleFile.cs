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
    ///     A file name such as "myFile.txt".
    /// </summary>
    string FileName { get; }

    long FileLength { get; }

    Task<long> GetLengthAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FileLength);
    }

    ValueTask<string[]> ReadAllLines();

    ValueTask<byte[]> ReadAllBytes();

    async Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = await ReadAllBytes().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    Task<Stream> OpenRead();

    async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await OpenRead().ConfigureAwait(false);
    }

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
        CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            (stream, writerCancellationToken) =>
                stream.WriteAsync(data, writerCancellationToken).AsTask(),
            cancellationToken);

    async Task AppendAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        byte[] existing = await ReadAllBytesAsync(cancellationToken).ConfigureAwait(false);
        await WriteAsync(
            async (stream, writerCancellationToken) =>
            {
                await stream.WriteAsync(existing, writerCancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(data, writerCancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }
}
