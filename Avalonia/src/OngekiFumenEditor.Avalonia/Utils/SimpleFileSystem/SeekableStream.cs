#nullable enable

using System.Buffers;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

internal sealed class SeekableStream : Stream
{
    private const int BufferSize = 81_920;

    private readonly Stream baseStream;
    private readonly MemoryStream cache = new();
    private readonly long length;
    private long position;
    private bool endOfStream;
    private bool isDisposed;

    public SeekableStream(Stream baseStream, long length)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        if (!baseStream.CanRead)
            throw new ArgumentException("Base stream must be readable.", nameof(baseStream));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        this.baseStream = baseStream;
        this.length = length;
    }

    public override bool CanRead => !isDisposed && baseStream.CanRead;

    public override bool CanSeek => !isDisposed;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return length;
        }
    }

    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            return position;
        }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateReadArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (buffer.IsEmpty || position >= length)
            return 0;

        var targetPosition = Math.Min(length, checked(position + buffer.Length));
        EnsureCached(targetPosition);
        return ReadFromCache(buffer);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || position >= length)
            return 0;

        var targetPosition = Math.Min(length, checked(position + buffer.Length));
        await EnsureCachedAsync(targetPosition, cancellationToken);
        return ReadFromCache(buffer.Span);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => checked(position + offset),
            SeekOrigin.End => checked(length + offset),
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
        };

        if (newPosition < 0)
            throw new IOException("Cannot seek to a negative position.");

        position = newPosition;
        return position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException("SeekableStream does not support SetLength.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("SeekableStream does not support Write.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !isDisposed)
        {
            isDisposed = true;
            baseStream.Dispose();
            cache.Dispose();
        }

        base.Dispose(disposing);
    }

    private int ReadFromCache(Span<byte> destination)
    {
        var available = Math.Min(cache.Length - position, destination.Length);
        if (available <= 0)
            return 0;

        cache.Position = position;
        var bytesRead = cache.Read(destination[..checked((int)available)]);
        position += bytesRead;
        cache.Position = cache.Length;
        return bytesRead;
    }

    private void EnsureCached(long targetPosition)
    {
        if (targetPosition <= cache.Length || endOfStream)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            cache.Position = cache.Length;
            while (cache.Length < targetPosition && !endOfStream)
            {
                var requested = (int)Math.Min(buffer.Length, targetPosition - cache.Length);
                var bytesRead = baseStream.Read(buffer, 0, requested);
                if (bytesRead == 0)
                {
                    endOfStream = true;
                    break;
                }

                cache.Write(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task EnsureCachedAsync(long targetPosition, CancellationToken cancellationToken)
    {
        if (targetPosition <= cache.Length || endOfStream)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            cache.Position = cache.Length;
            while (cache.Length < targetPosition && !endOfStream)
            {
                var requested = (int)Math.Min(buffer.Length, targetPosition - cache.Length);
                var bytesRead = await baseStream
                    .ReadAsync(buffer.AsMemory(0, requested), cancellationToken)
                    ;
                if (bytesRead == 0)
                {
                    endOfStream = true;
                    break;
                }

                await cache
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    ;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateReadArguments(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count)
            throw new ArgumentException("Offset and count exceed the buffer length.");
    }
}
