#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public interface ITemporaryFile : ITemporaryEntry
{
    Task<long> GetLengthAsync(CancellationToken cancellationToken = default);

    Task<byte[]> ReadAllBytesAsync(CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the file only after <paramref name="writer"/> completes successfully.
    /// Cancellation is ignored once the writer has completed and commit begins.
    /// </summary>
    Task WriteAsync(
        Func<Stream, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
