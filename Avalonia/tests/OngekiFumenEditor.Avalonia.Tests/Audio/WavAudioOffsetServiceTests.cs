using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Wave;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class WavAudioOffsetServiceTests
{
    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersSingletonWavAudioOffsetService()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IWavAudioOffsetService>();
        var second = provider.GetRequiredService<IWavAudioOffsetService>();

        Assert.IsType<DefaultWavAudioOffsetService>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task OffsetAsync_PositiveFractionalOffset_QuantizesToBlockAlignAndPreservesOddChunkPadding()
    {
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22, 0x31, 0x32 };
        var input = CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 4,
            bitsPerSample: 16,
            data: sourceFrames,
            chunksBeforeData: [new TestChunk("JUNK", [0xA1, 0xA2, 0xA3], 0x7E)]);
        using var inputFile = new TestSimpleFile("input.wav", input);
        using var outputFile = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputFile, outputFile, TimeSpan.FromMilliseconds(375));

        var output = outputFile.Content;
        var chunks = ReadChunks(output);
        var dataChunk = Assert.Single(chunks, chunk => chunk.Id == "data");
        var junkChunk = Assert.Single(chunks, chunk => chunk.Id == "JUNK");
        Assert.Equal(new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22, 0x31, 0x32 }, dataChunk.Data);
        Assert.Equal(new byte[] { 0xA1, 0xA2, 0xA3 }, junkChunk.Data);
        Assert.Equal((byte)0x7E, junkChunk.PaddingByte);
        Assert.Equal(0, dataChunk.Data.Length % 2);
        Assert.Equal((uint)(output.Length - 8), BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(4)));
    }

    [Fact]
    public async Task OffsetAsync_SimpleFiles_UsesStorageStreamsAndWritesAdjustedWave()
    {
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22 };
        using var input = new TestSimpleFile(
            "input.wav",
            CreateWave(1, 1, 4, 16, sourceFrames));
        using var output = new TestSimpleFile("output.wav", [0xDE, 0xAD]);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(250));

        var data = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22 }, data);
        Assert.Equal(1, input.OpenReadCount);
        Assert.Equal(1, output.OpenWriteCount);
    }

    [Fact]
    public async Task OffsetAsync_SameSimpleFile_StagesOutputUntilInputStreamIsClosed()
    {
        using var file = new TestSimpleFile(
            "same.wav",
            CreateWave(1, 1, 4, 16, [0x11, 0x12, 0x21, 0x22]));

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(file, file, TimeSpan.FromMilliseconds(250));

        var data = Assert.Single(ReadChunks(file.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22 }, data);
        Assert.Equal(1, file.OpenReadCount);
        Assert.Equal(1, file.OpenWriteCount);
    }

    [Fact]
    public async Task OffsetAsync_SameFullPathAcrossFileInstances_StagesOutput()
    {
        using var input = new TestSimpleFile(
            "input.wav",
            CreateWave(1, 1, 4, 16, [0x11, 0x12, 0x21, 0x22]),
            "provider://shared/same.wav");
        using var output = new TestSimpleFile(
            "output.wav",
            [],
            "provider://shared/same.wav",
            () => input.HasActiveRead);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(250));

        Assert.False(output.WasWriteOpenedWhileProbeWasTrue);
        var data = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22 }, data);
    }

    [Fact]
    public async Task OffsetAsync_DifferentNonLocalFilesWithSameName_StreamWithoutStaging()
    {
        using var input = new TestSimpleFile(
            "same.wav",
            CreateWave(1, 1, 4, 16, [0x11, 0x12, 0x21, 0x22]),
            "provider://input/same.wav");
        using var output = new TestSimpleFile(
            "same.wav",
            [],
            "provider://output/same.wav",
            () => input.HasActiveRead);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(250));

        Assert.True(output.WasWriteOpenedWhileProbeWasTrue);
        var data = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22 }, data);
    }

    [Fact]
    public async Task OffsetAsync_SimpleFiles_WritesToStorageStream()
    {
        using var input = new TestSimpleFile("input.wav", CreateWave(
            1,
            1,
            4,
            16,
            [0x11, 0x12, 0x21, 0x22, 0x31, 0x32]));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(-250));

        var data = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x21, 0x22, 0x31, 0x32 }, data);
        Assert.Equal(1, output.OpenWriteCount);
    }

    [Fact]
    public async Task OffsetAsync_InvalidSimpleInput_DoesNotOpenOrOverwriteStorageOutput()
    {
        using var input = new TestSimpleFile(
            "invalid.wav",
            CreateWave(1, 1, 4, 16, [0x01, 0x02, 0x03]));
        var originalTarget = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        using var output = new TestSimpleFile("existing.wav", originalTarget);

        var service = new DefaultWavAudioOffsetService();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.OffsetAsync(input, output, TimeSpan.FromSeconds(1)));

        Assert.Contains("BlockAlign", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, output.OpenWriteCount);
        Assert.Equal(originalTarget, output.Content);
    }

    [Fact]
    public async Task OffsetAsync_NegativeFractionalOffset_RemovesOnlyWholeFramesFromStart()
    {
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22, 0x31, 0x32 };
        using var input = new TestSimpleFile("input.wav", CreateWave(1, 1, 4, 16, sourceFrames));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(-375));

        var dataChunk = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data");
        Assert.Equal(new byte[] { 0x21, 0x22, 0x31, 0x32 }, dataChunk.Data);
        Assert.Equal(0, dataChunk.Data.Length % 2);
    }

    [Fact]
    public async Task OffsetAsync_ZeroOffsetAndSameFile_PreservesEveryInputByte()
    {
        var original = CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 8_000,
            bitsPerSample: 8,
            data: [0x80, 0x90, 0x70],
            chunksBeforeData: [new TestChunk("JUNK", [0x01, 0x02, 0x03], 0x7E)],
            chunksAfterData: [new TestChunk("LIST", [0x41, 0x42, 0x43], 0x55)],
            dataPaddingByte: 0xAB);
        using var file = new TestSimpleFile("in-place.wav", original);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(file, file, TimeSpan.Zero);

        Assert.Equal(original, file.Content);
    }

    [Fact]
    public async Task OffsetAsync_NegativeOffsetBeyondDuration_ProducesEmptyDataAndPreservesFollowingChunks()
    {
        using var input = new TestSimpleFile("input.wav", CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 4,
            bitsPerSample: 16,
            data: [0x11, 0x12, 0x21, 0x22],
            chunksAfterData: [new TestChunk("LIST", [0x51, 0x52, 0x53], 0x6A)]));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromSeconds(-10));

        var chunks = ReadChunks(output.Content);
        Assert.Empty(Assert.Single(chunks, chunk => chunk.Id == "data").Data);
        var listChunk = Assert.Single(chunks, chunk => chunk.Id == "LIST");
        Assert.Equal(new byte[] { 0x51, 0x52, 0x53 }, listChunk.Data);
        Assert.Equal((byte)0x6A, listChunk.PaddingByte);
    }

    [Fact]
    public async Task OffsetAsync_PositiveOffsetOnStereoFloat_PrependsOneZeroValuedSampleFrame()
    {
        var sourceFrames = FloatBytes(0.25f, -0.5f, 1.0f, -1.0f);
        using var input = new TestSimpleFile("input.wav", CreateWave(3, 2, 2, 32, sourceFrames));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(500));

        var chunks = ReadChunks(output.Content);
        var format = Assert.Single(chunks, chunk => chunk.Id == "fmt ").Data;
        var data = Assert.Single(chunks, chunk => chunk.Id == "data").Data;
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(format));
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(12)));
        Assert.Equal(new byte[8], data[..8]);
        Assert.Equal(sourceFrames, data[8..]);
    }

    [Fact]
    public async Task OffsetAsync_PositiveOffsetOnEightBitPcm_UsesUnsignedSilenceLevel()
    {
        using var input = new TestSimpleFile("input.wav", CreateWave(1, 1, 2, 8, [0x70, 0x90]));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(500));

        var data = Assert.Single(ReadChunks(output.Content), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x80, 0x70, 0x90 }, data);
    }

    [Fact]
    public async Task OffsetAsync_MisalignedPcmData_ThrowsAndPreservesExistingTarget()
    {
        var originalTarget = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        using var input = new TestSimpleFile("invalid.wav", CreateWave(1, 1, 4, 16, [0x01, 0x02, 0x03]));
        using var output = new TestSimpleFile("existing.wav", originalTarget);

        var service = new DefaultWavAudioOffsetService();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.OffsetAsync(input, output, TimeSpan.FromSeconds(1)));

        Assert.Contains("BlockAlign", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalTarget, output.Content);
        Assert.Equal(0, output.OpenWriteCount);
    }

    [Fact]
    public async Task OffsetAsync_UnsupportedCompressedFormat_ThrowsAndDoesNotCreateTarget()
    {
        using var input = new TestSimpleFile("unsupported.wav", CreateWave(6, 1, 8_000, 8, [0x01, 0x02]));
        using var output = new TestSimpleFile("output.wav", []);

        var service = new DefaultWavAudioOffsetService();
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.OffsetAsync(input, output, TimeSpan.Zero));

        Assert.Contains("0x0006", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, output.OpenWriteCount);
        Assert.Empty(output.Content);
    }

    [Fact]
    public async Task OffsetAsync_OutputWriteFails_LeavesExistingTargetUntouched()
    {
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22 };
        var originalTarget = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        using var input = new TestSimpleFile("input.wav", CreateWave(1, 1, 4, 16, sourceFrames));
        using var output = new FailingWriteSimpleFile("existing.wav", originalTarget);

        var service = new DefaultWavAudioOffsetService();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            service.OffsetAsync(input, output, TimeSpan.FromMilliseconds(250)));

        Assert.Contains("Simulated failure", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalTarget, output.Content);
        Assert.Equal(1, output.WriteCount);
    }

    private static byte[] CreateWave(
        ushort formatTag,
        ushort channels,
        uint sampleRate,
        ushort bitsPerSample,
        byte[] data,
        IReadOnlyList<TestChunk>? chunksBeforeData = null,
        IReadOnlyList<TestChunk>? chunksAfterData = null,
        byte dataPaddingByte = 0)
    {
        var blockAlign = checked((ushort)(channels * (bitsPerSample / 8)));
        var byteRate = checked(sampleRate * blockAlign);
        var format = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(format, formatTag);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(2), channels);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(4), sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(format.AsSpan(8), byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(12), blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(format.AsSpan(14), bitsPerSample);

        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0u);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            WriteChunk(writer, new TestChunk("fmt ", format));
            foreach (var chunk in chunksBeforeData ?? [])
                WriteChunk(writer, chunk);
            WriteChunk(writer, new TestChunk("data", data, dataPaddingByte));
            foreach (var chunk in chunksAfterData ?? [])
                WriteChunk(writer, chunk);
        }

        var result = memory.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), checked((uint)(result.Length - 8)));
        return result;
    }

    private static void WriteChunk(BinaryWriter writer, TestChunk chunk)
    {
        Assert.Equal(4, chunk.Id.Length);
        writer.Write(Encoding.ASCII.GetBytes(chunk.Id));
        writer.Write(checked((uint)chunk.Data.Length));
        writer.Write(chunk.Data);
        if ((chunk.Data.Length & 1) != 0)
            writer.Write(chunk.PaddingByte);
    }

    private static IReadOnlyList<ParsedChunk> ReadChunks(byte[] wave)
    {
        Assert.True(wave.Length >= 12);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wave, 8, 4));

        var riffEnd = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(4)) + 8);
        Assert.InRange(riffEnd, 12, wave.Length);
        var chunks = new List<ParsedChunk>();
        var position = 12;

        while (position < riffEnd)
        {
            Assert.True(riffEnd - position >= 8);
            var id = Encoding.ASCII.GetString(wave, position, 4);
            var size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(position + 4)));
            var dataOffset = position + 8;
            var paddedEnd = checked(dataOffset + size + (size & 1));
            Assert.InRange(paddedEnd, dataOffset, riffEnd);
            byte? paddingByte = (size & 1) == 0 ? null : wave[dataOffset + size];
            chunks.Add(new ParsedChunk(id, wave.AsSpan(dataOffset, size).ToArray(), paddingByte));
            position = paddedEnd;
        }

        Assert.Equal(riffEnd, position);
        return chunks;
    }

    private static byte[] FloatBytes(params float[] values)
    {
        var result = new byte[values.Length * sizeof(float)];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(i * sizeof(float)), values[i]);
        return result;
    }

    private sealed record TestChunk(string Id, byte[] Data, byte PaddingByte = 0);

    private sealed record ParsedChunk(string Id, byte[] Data, byte? PaddingByte);

    private sealed class TestSimpleFile(
        string fileName,
        byte[] initialContent,
        string? fullPath = null,
        Func<bool>? writeProbe = null) : ISimpleFile
    {
        private byte[] content = initialContent.ToArray();
        private int activeReadCount;

        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => fullPath ?? $"virtual/{fileName}";
        public string FileName => fileName;
        public long FileLength => content.LongLength;
        public byte[] Content => content.ToArray();
        public int OpenReadCount { get; private set; }
        public int OpenWriteCount { get; private set; }
        public bool HasActiveRead => activeReadCount != 0;
        public bool WasWriteOpenedWhileProbeWasTrue { get; private set; }

        public ValueTask<string[]> ReadAllLines()
        {
            return ValueTask.FromResult(
                Encoding.UTF8.GetString(content).Split(["\r\n", "\n"], StringSplitOptions.None));
        }

        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Content);

        public Task<Stream> OpenRead()
        {
            OpenReadCount++;
            activeReadCount++;
            return Task.FromResult<Stream>(new TrackingReadStream(
                content,
                () => activeReadCount--));
        }

        public Task<Stream> OpenWrite()
        {
            if (activeReadCount != 0)
                throw new IOException("The backing file cannot be opened for writing while it is being read.");

            OpenWriteCount++;
            WasWriteOpenedWhileProbeWasTrue = writeProbe?.Invoke() ?? false;
            return Task.FromResult<Stream>(new CommitMemoryStream(bytes => content = bytes));
        }

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            cancellationToken.ThrowIfCancellationRequested();
            if (activeReadCount != 0)
                throw new IOException("The backing file cannot be opened for writing while it is being read.");

            OpenWriteCount++;
            WasWriteOpenedWhileProbeWasTrue = writeProbe?.Invoke() ?? false;
            await using var stream = new MemoryStream();
            await writer(stream, cancellationToken);
            await stream.FlushAsync(CancellationToken.None);
            content = stream.ToArray();
        }

        public void Dispose()
        {
        }
    }

    private sealed class TrackingReadStream(byte[] content, Action onDispose)
        : MemoryStream(content, writable: false)
    {
        private bool isDisposed;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !isDisposed)
            {
                isDisposed = true;
                onDispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class CommitMemoryStream(Action<byte[]> commit) : MemoryStream
    {
        private bool isCommitted;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !isCommitted)
            {
                isCommitted = true;
                commit(ToArray());
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FailingWriteSimpleFile(string fileName, byte[] initialContent) : ISimpleFile
    {
        private readonly byte[] originalContent = initialContent.ToArray();

        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"virtual/{fileName}";
        public string FileName => fileName;
        public long FileLength => originalContent.LongLength;
        public byte[] Content => originalContent.ToArray();
        public int WriteCount { get; private set; }

        public ValueTask<string[]> ReadAllLines() =>
            ValueTask.FromResult(Encoding.UTF8.GetString(originalContent).Split(["\r\n", "\n"], StringSplitOptions.None));

        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Content);

        public Task<Stream> OpenRead() => Task.FromResult<Stream>(new MemoryStream(originalContent, writable: false));

        public Task<Stream> OpenWrite() => throw new IOException("Simulated failure replacing output.");

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writer);
            WriteCount++;
            await using var staged = new MemoryStream();
            await writer(staged, cancellationToken);
            throw new IOException("Simulated failure replacing output.");
        }

        public void Dispose()
        {
        }
    }
}
