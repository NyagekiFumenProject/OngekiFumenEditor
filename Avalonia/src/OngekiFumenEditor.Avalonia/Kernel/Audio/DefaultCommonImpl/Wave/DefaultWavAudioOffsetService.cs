#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Wave;

[RegisterSingleton<IWavAudioOffsetService>]
internal sealed class DefaultWavAudioOffsetService : IWavAudioOffsetService
{
    private const int StreamBufferSize = 81_920;
    private const uint RiffFourCc = 0x4646_4952;
    private const uint WaveFourCc = 0x4556_4157;
    private const uint FormatFourCc = 0x2074_6D66;
    private const uint DataFourCc = 0x6174_6164;
    private const ushort PcmFormatTag = 0x0001;
    private const ushort IeeeFloatFormatTag = 0x0003;
    private const ushort ExtensibleFormatTag = 0xFFFE;
    private static readonly Guid PcmSubFormat = new(
        0x00000001, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);
    private static readonly Guid IeeeFloatSubFormat = new(
        0x00000003, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71);

    private readonly Action<string, string> commitTemporaryFile;

    public DefaultWavAudioOffsetService()
        : this(CommitTemporaryFile)
    {
    }

    internal DefaultWavAudioOffsetService(Action<string, string> commitTemporaryFile)
    {
        ArgumentNullException.ThrowIfNull(commitTemporaryFile);
        this.commitTemporaryFile = commitTemporaryFile;
    }

    public async Task OffsetAsync(
        ISimpleFile inputWavFile,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputWavFile);
        ArgumentNullException.ThrowIfNull(outputWavFile);

        if (RefersToSameFile(inputWavFile, outputWavFile))
        {
            await using var stagedOutput = await RenderToMemoryAsync(
                inputWavFile,
                offset,
                cancellationToken);
            await CommitStagedOutputAsync(stagedOutput, outputWavFile, cancellationToken);
            return;
        }

        await using var input = await inputWavFile.OpenRead();
        await OffsetToStorageFileAsync(input, outputWavFile, offset, cancellationToken);
    }

    public async Task OffsetAsync(
        string inputWavFilePath,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputWavFilePath);
        ArgumentNullException.ThrowIfNull(outputWavFile);

        if (RefersToSameFile(inputWavFilePath, outputWavFile))
        {
            await using var stagedOutput = await RenderToMemoryAsync(
                inputWavFilePath,
                offset,
                cancellationToken);
            await CommitStagedOutputAsync(stagedOutput, outputWavFile, cancellationToken);
            return;
        }

        await using var input = OpenInput(Path.GetFullPath(inputWavFilePath));
        await OffsetToStorageFileAsync(input, outputWavFile, offset, cancellationToken);
    }

    public async Task OffsetAsync(
        string inputWavFilePath,
        string outputWavFilePath,
        TimeSpan offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputWavFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputWavFilePath);

        var inputPath = Path.GetFullPath(inputWavFilePath);
        var outputPath = Path.GetFullPath(outputWavFilePath);
        string? temporaryPath = null;
        var isCommitted = false;

        try
        {
            await using (var input = OpenInput(inputPath))
            {
                var operation = await PrepareOffsetAsync(input, offset, cancellationToken);
                var outputDirectory = Path.GetDirectoryName(outputPath)!;
                Directory.CreateDirectory(outputDirectory);

                await using (var output = CreateTemporaryOutput(outputPath, out temporaryPath))
                {
                    await WriteOffsetAsync(input, output, operation, cancellationToken);

                    await output.FlushAsync(cancellationToken);
                    output.Flush(flushToDisk: true);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            commitTemporaryFile(temporaryPath!, outputPath);
            isCommitted = true;
        }
        finally
        {
            if (!isCommitted && temporaryPath is not null)
                TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static async Task OffsetToStorageFileAsync(
        Stream input,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        var operation = await PrepareOffsetAsync(input, offset, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await outputWavFile.WriteAsync(
            (output, token) => WriteOffsetAsync(input, output, operation, token),
            cancellationToken);
    }

    private static async Task<MemoryStream> RenderToMemoryAsync(
        ISimpleFile inputWavFile,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        await using var input = await inputWavFile.OpenRead();
        return await RenderToMemoryAsync(input, offset, cancellationToken);
    }

    private static async Task<MemoryStream> RenderToMemoryAsync(
        string inputWavFilePath,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        await using var input = OpenInput(Path.GetFullPath(inputWavFilePath));
        return await RenderToMemoryAsync(input, offset, cancellationToken);
    }

    private static async Task<MemoryStream> RenderToMemoryAsync(
        Stream input,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        var stagedOutput = new MemoryStream();
        try
        {
            var operation = await PrepareOffsetAsync(input, offset, cancellationToken);
            await WriteOffsetAsync(input, stagedOutput, operation, cancellationToken);
            stagedOutput.Position = 0;
            return stagedOutput;
        }
        catch
        {
            await stagedOutput.DisposeAsync();
            throw;
        }
    }

    private static async Task CommitStagedOutputAsync(
        Stream stagedOutput,
        ISimpleFile outputWavFile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await outputWavFile.WriteAsync(
            (output, token) => stagedOutput.CopyToAsync(output, token),
            cancellationToken);
    }

    private static bool RefersToSameFile(ISimpleFile inputFile, ISimpleFile outputFile)
    {
        if (ReferenceEquals(inputFile, outputFile))
            return true;

        if (!string.IsNullOrWhiteSpace(inputFile.LocalPath) &&
            !string.IsNullOrWhiteSpace(outputFile.LocalPath))
        {
            return PathsEqual(inputFile.LocalPath, outputFile.LocalPath);
        }

        return string.IsNullOrWhiteSpace(inputFile.LocalPath) &&
               string.IsNullOrWhiteSpace(outputFile.LocalPath) &&
               string.Equals(inputFile.FullPath, outputFile.FullPath, StringComparison.Ordinal);
    }

    private static bool RefersToSameFile(string inputFilePath, ISimpleFile outputFile)
    {
        return !string.IsNullOrWhiteSpace(outputFile.LocalPath) &&
               PathsEqual(inputFilePath, outputFile.LocalPath);
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    private static async Task<OffsetOperation> PrepareOffsetAsync(
        Stream input,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        if (!input.CanRead)
            throw new ArgumentException("The input WAV stream must be readable.", nameof(input));
        if (!input.CanSeek)
            throw new ArgumentException("The input WAV stream must be seekable.", nameof(input));

        var layout = await ReadLayoutAsync(input, cancellationToken);
        return new OffsetOperation(layout, CalculateAdjustment(layout, offset));
    }

    private static async Task WriteOffsetAsync(
        Stream input,
        Stream output,
        OffsetOperation operation,
        CancellationToken cancellationToken)
    {
        if (!output.CanWrite)
            throw new ArgumentException("The output WAV stream must be writable.", nameof(output));

        if (operation.Adjustment.IsByteExactCopy)
        {
            input.Position = 0;
            await CopyExactlyAsync(input, output, operation.Layout.FileLength, cancellationToken);
        }
        else
        {
            await WriteAdjustedWaveAsync(
                input,
                output,
                operation.Layout,
                operation.Adjustment,
                cancellationToken);
        }
    }

    private static FileStream OpenInput(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        StreamBufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static FileStream CreateTemporaryOutput(string outputPath, out string temporaryPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath)!;
        var outputFileName = Path.GetFileName(outputPath);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = Path.Combine(
                outputDirectory,
                $".{outputFileName}.{Guid.NewGuid():N}.tmp");

            try
            {
                var stream = new FileStream(
                    candidate,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    StreamBufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                temporaryPath = candidate;
                return stream;
            }
            catch (IOException) when (File.Exists(candidate))
            {
            }
        }

        throw new IOException($"Unable to create a temporary WAV file beside '{outputPath}'.");
    }

    private static void CommitTemporaryFile(string temporaryPath, string outputPath)
    {
        // The temporary file is created in the destination directory so this overwrite is a same-volume rename.
        File.Move(temporaryPath, outputPath, overwrite: true);
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch
        {
            // The destination was never replaced; cleanup failure must not hide the original exception.
        }
    }

    private static async Task<WaveLayout> ReadLayoutAsync(Stream input, CancellationToken cancellationToken)
    {
        if (input.Length < 12)
            throw new InvalidDataException("The file is too short to contain a RIFF/WAVE header.");

        var riffHeader = new byte[12];
        input.Position = 0;
        await input.ReadExactlyAsync(riffHeader.AsMemory(), cancellationToken);

        if (BinaryPrimitives.ReadUInt32LittleEndian(riffHeader) != RiffFourCc ||
            BinaryPrimitives.ReadUInt32LittleEndian(riffHeader.AsSpan(8)) != WaveFourCc)
        {
            throw new InvalidDataException("The input file is not a RIFF/WAVE file.");
        }

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(riffHeader.AsSpan(4));
        var riffEnd = 8L + riffSize;
        if (riffEnd < 12 || riffEnd > input.Length)
            throw new InvalidDataException("The RIFF container size exceeds the input file length.");

        WaveFormatInfo? format = null;
        DataChunkInfo? dataChunk = null;
        var chunkHeader = new byte[8];
        var position = 12L;

        while (position < riffEnd)
        {
            if (riffEnd - position < chunkHeader.Length)
                throw new InvalidDataException("The RIFF container ends inside a chunk header.");

            input.Position = position;
            await input.ReadExactlyAsync(chunkHeader.AsMemory(), cancellationToken);

            var chunkId = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader.AsSpan(4));
            var chunkDataOffset = checked(position + chunkHeader.Length);
            var paddedChunkSize = (long)chunkSize + (chunkSize & 1u);
            var nextChunkOffset = checked(chunkDataOffset + paddedChunkSize);
            if (nextChunkOffset > riffEnd)
                throw new InvalidDataException("A RIFF chunk exceeds the declared container size.");

            if (chunkId == FormatFourCc)
            {
                if (format is not null)
                    throw new InvalidDataException("The WAVE file contains more than one format chunk.");

                format = await ReadFormatAsync(input, chunkDataOffset, chunkSize, cancellationToken);
            }
            else if (chunkId == DataFourCc)
            {
                if (dataChunk is not null)
                    throw new NotSupportedException("WAVE files with multiple data chunks are not supported.");

                dataChunk = new DataChunkInfo(position, chunkDataOffset, chunkSize, nextChunkOffset);
            }

            position = nextChunkOffset;
        }

        if (format is null)
            throw new InvalidDataException("The WAVE file does not contain a format chunk.");
        if (dataChunk is null)
            throw new InvalidDataException("The WAVE file does not contain a data chunk.");
        if (dataChunk.Value.Size % format.Value.BlockAlign != 0)
            throw new InvalidDataException("The WAVE data size is not aligned to BlockAlign.");

        return new WaveLayout(input.Length, riffSize, format.Value, dataChunk.Value);
    }

    private static async Task<WaveFormatInfo> ReadFormatAsync(
        Stream input,
        long formatOffset,
        uint formatSize,
        CancellationToken cancellationToken)
    {
        if (formatSize < 16)
            throw new InvalidDataException("The WAVE format chunk is shorter than 16 bytes.");

        var formatBuffer = new byte[(int)Math.Min(formatSize, 40u)];
        input.Position = formatOffset;
        await input.ReadExactlyAsync(formatBuffer.AsMemory(), cancellationToken);

        var formatTag = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer);
        var channelCount = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer.AsSpan(2));
        var sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(formatBuffer.AsSpan(4));
        var byteRate = BinaryPrimitives.ReadUInt32LittleEndian(formatBuffer.AsSpan(8));
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer.AsSpan(12));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer.AsSpan(14));
        var encoding = formatTag switch
        {
            PcmFormatTag => WaveEncoding.Pcm,
            IeeeFloatFormatTag => WaveEncoding.IeeeFloat,
            ExtensibleFormatTag => ReadExtensibleEncoding(formatBuffer, formatSize, bitsPerSample),
            _ => throw new NotSupportedException($"WAVE format tag 0x{formatTag:X4} is not supported.")
        };

        if (channelCount == 0)
            throw new InvalidDataException("The WAVE channel count must be greater than zero.");
        if (sampleRate == 0)
            throw new InvalidDataException("The WAVE sample rate must be greater than zero.");
        if (blockAlign == 0)
            throw new InvalidDataException("The WAVE BlockAlign value must be greater than zero.");

        var isSupportedBitDepth = encoding switch
        {
            WaveEncoding.Pcm => bitsPerSample is 8 or 16 or 24 or 32,
            WaveEncoding.IeeeFloat => bitsPerSample is 32 or 64,
            _ => false
        };
        if (!isSupportedBitDepth)
            throw new NotSupportedException($"{encoding} WAVE files with {bitsPerSample} bits per sample are not supported.");

        var expectedBlockAlign = checked((uint)channelCount * (uint)(bitsPerSample / 8));
        if (expectedBlockAlign != blockAlign)
            throw new InvalidDataException("The WAVE BlockAlign value does not match its channel count and bit depth.");

        var expectedByteRate = checked((ulong)sampleRate * blockAlign);
        if (expectedByteRate > uint.MaxValue || byteRate != expectedByteRate)
            throw new InvalidDataException("The WAVE byte rate does not match its sample rate and BlockAlign.");

        return new WaveFormatInfo(encoding, sampleRate, blockAlign, bitsPerSample);
    }

    private static WaveEncoding ReadExtensibleEncoding(
        ReadOnlySpan<byte> formatBuffer,
        uint formatSize,
        ushort bitsPerSample)
    {
        if (formatSize < 40 || formatBuffer.Length < 40)
            throw new InvalidDataException("The extensible WAVE format chunk is shorter than 40 bytes.");

        var extraSize = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer[16..]);
        var validBitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(formatBuffer[18..]);
        if (extraSize < 22 || formatSize < 18u + extraSize)
            throw new InvalidDataException("The extensible WAVE format metadata is incomplete.");
        if (validBitsPerSample == 0 || validBitsPerSample > bitsPerSample)
            throw new InvalidDataException("The extensible WAVE valid bit depth is invalid.");

        var subFormat = new Guid(formatBuffer.Slice(24, 16));
        if (subFormat == PcmSubFormat)
            return WaveEncoding.Pcm;
        if (subFormat == IeeeFloatSubFormat)
            return WaveEncoding.IeeeFloat;

        throw new NotSupportedException($"WAVE extensible sub-format '{subFormat}' is not supported.");
    }

    private static DataAdjustment CalculateAdjustment(WaveLayout layout, TimeSpan offset)
    {
        var requestedFrameCount = decimal.Truncate(
            decimal.Abs((decimal)offset.Ticks) * layout.Format.SampleRate / TimeSpan.TicksPerSecond);
        if (requestedFrameCount == 0)
            return DataAdjustment.ByteExact(layout.DataChunk.Size);

        if (offset > TimeSpan.Zero)
        {
            var maximumAdditionalFrames = (uint.MaxValue - layout.DataChunk.Size) / layout.Format.BlockAlign;
            if (requestedFrameCount > maximumAdditionalFrames)
                throw new NotSupportedException("The requested positive offset would exceed the RIFF/WAVE 4 GiB size limit.");

            var bytesToPrepend = checked((uint)((ulong)requestedFrameCount * layout.Format.BlockAlign));
            return new DataAdjustment(
                bytesToPrepend,
                0,
                checked(layout.DataChunk.Size + bytesToPrepend),
                false);
        }

        var existingFrameCount = layout.DataChunk.Size / layout.Format.BlockAlign;
        var bytesToSkip = requestedFrameCount >= existingFrameCount
            ? layout.DataChunk.Size
            : checked((uint)((ulong)requestedFrameCount * layout.Format.BlockAlign));
        return new DataAdjustment(0, bytesToSkip, layout.DataChunk.Size - bytesToSkip, false);
    }

    private static async Task WriteAdjustedWaveAsync(
        Stream input,
        Stream output,
        WaveLayout layout,
        DataAdjustment adjustment,
        CancellationToken cancellationToken)
    {
        var oldPaddedDataSize = (long)layout.DataChunk.Size + (layout.DataChunk.Size & 1u);
        var newPaddedDataSize = (long)adjustment.OutputDataSize + (adjustment.OutputDataSize & 1u);
        var outputRiffSize = checked((long)layout.RiffSize - oldPaddedDataSize + newPaddedDataSize);
        if (outputRiffSize is < 4 or > uint.MaxValue)
            throw new NotSupportedException("The adjusted audio cannot be represented by a RIFF/WAVE container.");

        input.Position = 0;
        await CopyExactlyAsync(input, output, 4, cancellationToken);
        input.Position = 8;
        await WriteUInt32Async(output, (uint)outputRiffSize, cancellationToken);

        var dataSizeFieldOffset = layout.DataChunk.HeaderOffset + 4;
        await CopyExactlyAsync(input, output, dataSizeFieldOffset - input.Position, cancellationToken);
        input.Position += 4;
        await WriteUInt32Async(output, adjustment.OutputDataSize, cancellationToken);

        if (adjustment.BytesToPrepend != 0)
        {
            await WriteSilenceAsync(
                output,
                adjustment.BytesToPrepend,
                layout.Format,
                cancellationToken);
        }

        input.Position = layout.DataChunk.DataOffset + adjustment.BytesToSkip;
        var inputDataBytesToCopy = (long)layout.DataChunk.Size - adjustment.BytesToSkip;
        await CopyExactlyAsync(input, output, inputDataBytesToCopy, cancellationToken);

        if ((adjustment.OutputDataSize & 1u) != 0)
            await output.WriteAsync(new byte[1], cancellationToken);

        input.Position = layout.DataChunk.PaddedEndOffset;
        await CopyExactlyAsync(
            input,
            output,
            layout.FileLength - layout.DataChunk.PaddedEndOffset,
            cancellationToken);
    }

    private static async Task WriteSilenceAsync(
        Stream output,
        uint byteCount,
        WaveFormatInfo format,
        CancellationToken cancellationToken)
    {
        var alignedBufferSize = Math.Max(
            format.BlockAlign,
            StreamBufferSize / format.BlockAlign * format.BlockAlign);
        var buffer = ArrayPool<byte>.Shared.Rent(alignedBufferSize);

        try
        {
            var silenceByte = format.Encoding == WaveEncoding.Pcm && format.BitsPerSample == 8
                ? (byte)0x80
                : (byte)0x00;
            buffer.AsSpan(0, alignedBufferSize).Fill(silenceByte);

            var remaining = (long)byteCount;
            while (remaining > 0)
            {
                var writeSize = (int)Math.Min(remaining, alignedBufferSize);
                await output.WriteAsync(buffer.AsMemory(0, writeSize), cancellationToken);
                remaining -= writeSize;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task CopyExactlyAsync(
        Stream input,
        Stream output,
        long byteCount,
        CancellationToken cancellationToken)
    {
        if (byteCount < 0)
            throw new InvalidDataException("A WAVE chunk offset is invalid.");

        var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            var remaining = byteCount;
            while (remaining > 0)
            {
                var readSize = (int)Math.Min(remaining, buffer.Length);
                var bytesRead = await input.ReadAsync(buffer.AsMemory(0, readSize), cancellationToken);
                if (bytesRead == 0)
                    throw new EndOfStreamException("The WAVE file ended while it was being copied.");

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                remaining -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task WriteUInt32Async(
        Stream output,
        uint value,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        await output.WriteAsync(buffer, cancellationToken);
    }

    private enum WaveEncoding
    {
        Pcm,
        IeeeFloat
    }

    private readonly record struct WaveFormatInfo(
        WaveEncoding Encoding,
        uint SampleRate,
        ushort BlockAlign,
        ushort BitsPerSample);

    private readonly record struct DataChunkInfo(
        long HeaderOffset,
        long DataOffset,
        uint Size,
        long PaddedEndOffset);

    private readonly record struct WaveLayout(
        long FileLength,
        uint RiffSize,
        WaveFormatInfo Format,
        DataChunkInfo DataChunk);

    private readonly record struct OffsetOperation(
        WaveLayout Layout,
        DataAdjustment Adjustment);

    private readonly record struct DataAdjustment(
        uint BytesToPrepend,
        uint BytesToSkip,
        uint OutputDataSize,
        bool IsByteExactCopy)
    {
        public static DataAdjustment ByteExact(uint dataSize) => new(0, 0, dataSize, true);
    }
}
