#nullable enable

using System.Buffers;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

/// <summary>
///     Compares two AWB payloads by length first, then a deterministic sampling pre-check for
///     large files, and always finishes with a full streaming byte comparison. Sampling can
///     only prove difference early; sameness is decided by the complete comparison alone.
/// </summary>
public static class AwbContentComparer
{
    private const int StreamBufferLength = 81_920;
    private const int SampleLength = 4096;
    private const int LargeFileSampleCount = 16;
    private const long LargeFileThreshold = 4 * 1024 * 1024;

    public static async Task<bool> AreContentsEqualAsync(
        ISimpleFile left,
        ISimpleFile right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftLength = await left.GetLengthAsync(cancellationToken).ConfigureAwait(false);
        var rightLength = await right.GetLengthAsync(cancellationToken).ConfigureAwait(false);
        if (leftLength != rightLength)
            return false;
        if (leftLength == 0)
            return true;

        await using var leftStream = await left.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        await using var rightStream = await right.OpenReadAsync(cancellationToken).ConfigureAwait(false);

        if (leftStream.CanSeek && rightStream.CanSeek && leftLength > LargeFileThreshold &&
            await HasSamplingMismatchAsync(
                leftStream,
                rightStream,
                leftLength,
                cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        return await StreamsAreEqualAsync(
            leftStream,
            rightStream,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> HasSamplingMismatchAsync(
        Stream leftStream,
        Stream rightStream,
        long length,
        CancellationToken cancellationToken)
    {
        var sampleSpan = length - SampleLength;
        if (sampleSpan <= 0)
            return false;

        // xorshift64* keeps the sampling offsets reproducible on every runtime and platform.
        var state = 0x9E3779B97F4A7C15UL ^ (ulong)length;
        var rentedLeft = ArrayPool<byte>.Shared.Rent(SampleLength);
        var rentedRight = ArrayPool<byte>.Shared.Rent(SampleLength);
        try
        {
            for (var sampleIndex = 0; sampleIndex < LargeFileSampleCount; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                var offset = (long)((state ^ (state >> 32)) % (ulong)sampleSpan);

                leftStream.Seek(offset, SeekOrigin.Begin);
                rightStream.Seek(offset, SeekOrigin.Begin);
                var leftRead = await ReadFilledAsync(leftStream, rentedLeft, cancellationToken).ConfigureAwait(false);
                var rightRead = await ReadFilledAsync(rightStream, rentedRight, cancellationToken).ConfigureAwait(false);
                if (leftRead != rightRead ||
                    !rentedLeft.AsSpan(0, leftRead).SequenceEqual(rentedRight.AsSpan(0, rightRead)))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedLeft);
            ArrayPool<byte>.Shared.Return(rentedRight);
        }
    }

    private static async Task<bool> StreamsAreEqualAsync(
        Stream leftStream,
        Stream rightStream,
        CancellationToken cancellationToken)
    {
        leftStream.Seek(0, SeekOrigin.Begin);
        rightStream.Seek(0, SeekOrigin.Begin);

        var rentedLeft = ArrayPool<byte>.Shared.Rent(StreamBufferLength);
        var rentedRight = ArrayPool<byte>.Shared.Rent(StreamBufferLength);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var leftRead = await leftStream.ReadAsync(
                    rentedLeft.AsMemory(0, StreamBufferLength),
                    cancellationToken).ConfigureAwait(false);
                var rightRead = await rightStream.ReadAsync(
                    rentedRight.AsMemory(0, StreamBufferLength),
                    cancellationToken).ConfigureAwait(false);
                if (leftRead != rightRead)
                    return false;
                if (leftRead == 0)
                    return true;
                if (!rentedLeft.AsSpan(0, leftRead).SequenceEqual(rentedRight.AsSpan(0, rightRead)))
                    return false;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedLeft);
            ArrayPool<byte>.Shared.Return(rentedRight);
        }
    }

    private static async Task<int> ReadFilledAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < SampleLength)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(total, SampleLength - total),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
        }

        return total;
    }
}
