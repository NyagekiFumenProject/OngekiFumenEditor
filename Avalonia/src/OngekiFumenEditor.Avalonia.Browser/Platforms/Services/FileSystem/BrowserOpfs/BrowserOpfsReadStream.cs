#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.FileSystem.BrowserOpfs;

[SupportedOSPlatform("browser")]
internal sealed class BrowserOpfsReadStream : Stream
{
    private const int MaximumChunkLength = 256 * 1024;
    private readonly long length;
    private int handle;
    private long position;

    private BrowserOpfsReadStream(int handle, long length)
    {
        this.handle = handle;
        this.length = length;
    }

    public override bool CanRead => handle != 0;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public static async Task<BrowserOpfsReadStream> OpenAsync(
        BrowserOpfsManifestEntryDto entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.Size is not { } size || entry.LastModified is not { } lastModified)
            throw new IOException($"OPFS manifest metadata is incomplete for '{entry.Path}'.");

        int handle = await BrowserOpfsInterop.OpenReadAsync(entry.Path, size, lastModified);
        if (cancellationToken.IsCancellationRequested)
        {
            BrowserOpfsInterop.CloseRead(handle);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return new BrowserOpfsReadStream(handle, size);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Browser OPFS reads are asynchronous.");

    public override int Read(Span<byte> buffer) =>
        throw new NotSupportedException("Browser OPFS reads are asynchronous.");

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(handle == 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length == 0 || position >= length)
            return 0;

        int requestedLength = (int)Math.Min(
            Math.Min(buffer.Length, MaximumChunkLength),
            length - position);
        using JSObject result = await BrowserOpfsInterop.ReadChunkAsync(handle, requestedLength);
        byte[] bytes = result.GetPropertyAsByteArray("data")
                       ?? throw new IOException("OPFS returned no data for a file read chunk.");
        if (bytes.Length == 0 && position < length)
            throw new EndOfStreamException("OPFS returned an unexpected end of file.");
        if (bytes.Length > requestedLength)
            throw new IOException("OPFS returned more file data than requested.");

        bytes.AsSpan().CopyTo(buffer.Span);
        position += bytes.Length;
        cancellationToken.ThrowIfCancellationRequested();
        return bytes.Length;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (handle != 0)
        {
            BrowserOpfsInterop.CloseRead(handle);
            handle = 0;
        }
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
