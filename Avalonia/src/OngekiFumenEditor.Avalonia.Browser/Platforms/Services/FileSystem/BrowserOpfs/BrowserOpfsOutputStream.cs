#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.FileSystem.BrowserOpfs;

[SupportedOSPlatform("browser")]
internal sealed class BrowserOpfsOutputStream : Stream
{
    private static int nextBufferHandle;
    private readonly int outputHandle;
    private bool completed;
    private long position;

    public BrowserOpfsOutputStream(int outputHandle)
    {
        if (outputHandle <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputHandle));
        this.outputHandle = outputHandle;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => !completed;
    public override long Length => position;

    public override long Position
    {
        get => position;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(completed, this);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        cancellationToken.ThrowIfCancellationRequested();
        await BrowserOpfsInterop.FlushDownloadAsync(outputHandle);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count)
            throw new ArgumentException("The write range exceeds the supplied buffer.");
        ObjectDisposedException.ThrowIf(completed, this);
        if (count == 0)
            return;

        int bufferHandle = Interlocked.Increment(ref nextBufferHandle);
        try
        {
            BrowserOpfsInterop.SetWriteBuffer(bufferHandle, buffer.AsSpan(offset, count), count);
            BrowserOpfsInterop.QueueDownloadBuffer(outputHandle, bufferHandle);
            position += count;
        }
        finally
        {
            BrowserOpfsInterop.ReleaseWriteBuffer(bufferHandle);
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
            return;
        byte[] copy = buffer.ToArray();
        Write(copy, 0, copy.Length);
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length == 0)
            return;

        int bufferHandle = Interlocked.Increment(ref nextBufferHandle);
        try
        {
            SetInteropBuffer(bufferHandle, buffer);
            await BrowserOpfsInterop.WriteDownloadBufferAsync(outputHandle, bufferHandle);
            position += buffer.Length;
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            BrowserOpfsInterop.ReleaseWriteBuffer(bufferHandle);
        }
    }

    public async Task CommitAsync()
    {
        ObjectDisposedException.ThrowIf(completed, this);
        await BrowserOpfsInterop.CloseDownloadAsync(outputHandle);
        completed = true;
    }

    public async Task AbortAsync()
    {
        if (completed)
            return;
        await BrowserOpfsInterop.AbortDownloadAsync(outputHandle);
        completed = true;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    private static void SetInteropBuffer(int handle, ReadOnlyMemory<byte> buffer)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment) && segment.Array is not null)
        {
            BrowserOpfsInterop.SetWriteBuffer(
                handle,
                segment.Array.AsSpan(segment.Offset, segment.Count),
                segment.Count);
            return;
        }

        byte[] copy = buffer.ToArray();
        BrowserOpfsInterop.SetWriteBuffer(handle, copy, copy.Length);
    }
}
