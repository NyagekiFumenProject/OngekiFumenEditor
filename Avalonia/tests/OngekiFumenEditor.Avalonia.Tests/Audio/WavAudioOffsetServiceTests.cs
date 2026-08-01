using System.Buffers.Binary;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Wave;
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
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("output.wav");
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22, 0x31, 0x32 };
        var input = CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 4,
            bitsPerSample: 16,
            data: sourceFrames,
            chunksBeforeData: [new TestChunk("JUNK", [0xA1, 0xA2, 0xA3], 0x7E)]);
        await File.WriteAllBytesAsync(inputPath, input);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputPath, outputPath, TimeSpan.FromMilliseconds(375));

        var output = await File.ReadAllBytesAsync(outputPath);
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
    public async Task OffsetAsync_NegativeFractionalOffset_RemovesOnlyWholeFramesFromStart()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("output.wav");
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22, 0x31, 0x32 };
        await File.WriteAllBytesAsync(inputPath, CreateWave(1, 1, 4, 16, sourceFrames));

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputPath, outputPath, TimeSpan.FromMilliseconds(-375));

        var output = await File.ReadAllBytesAsync(outputPath);
        var dataChunk = Assert.Single(ReadChunks(output), chunk => chunk.Id == "data");
        Assert.Equal(new byte[] { 0x21, 0x22, 0x31, 0x32 }, dataChunk.Data);
        Assert.Equal(0, dataChunk.Data.Length % 2);
    }

    [Fact]
    public async Task OffsetAsync_ZeroOffsetAndSamePath_PreservesEveryInputByte()
    {
        using var directory = new TemporaryDirectory();
        var wavePath = directory.File("in-place.wav");
        var original = CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 8_000,
            bitsPerSample: 8,
            data: [0x80, 0x90, 0x70],
            chunksBeforeData: [new TestChunk("JUNK", [0x01, 0x02, 0x03], 0x7E)],
            chunksAfterData: [new TestChunk("LIST", [0x41, 0x42, 0x43], 0x55)],
            dataPaddingByte: 0xAB);
        await File.WriteAllBytesAsync(wavePath, original);

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(wavePath, wavePath, TimeSpan.Zero);

        Assert.Equal(original, await File.ReadAllBytesAsync(wavePath));
    }

    [Fact]
    public async Task OffsetAsync_NegativeOffsetBeyondDuration_ProducesEmptyDataAndPreservesFollowingChunks()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("output.wav");
        await File.WriteAllBytesAsync(inputPath, CreateWave(
            formatTag: 1,
            channels: 1,
            sampleRate: 4,
            bitsPerSample: 16,
            data: [0x11, 0x12, 0x21, 0x22],
            chunksAfterData: [new TestChunk("LIST", [0x51, 0x52, 0x53], 0x6A)]));

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputPath, outputPath, TimeSpan.FromSeconds(-10));

        var chunks = ReadChunks(await File.ReadAllBytesAsync(outputPath));
        Assert.Empty(Assert.Single(chunks, chunk => chunk.Id == "data").Data);
        var listChunk = Assert.Single(chunks, chunk => chunk.Id == "LIST");
        Assert.Equal(new byte[] { 0x51, 0x52, 0x53 }, listChunk.Data);
        Assert.Equal((byte)0x6A, listChunk.PaddingByte);
    }

    [Fact]
    public async Task OffsetAsync_PositiveOffsetOnStereoFloat_PrependsOneZeroValuedSampleFrame()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("output.wav");
        var sourceFrames = FloatBytes(0.25f, -0.5f, 1.0f, -1.0f);
        await File.WriteAllBytesAsync(inputPath, CreateWave(3, 2, 2, 32, sourceFrames));

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputPath, outputPath, TimeSpan.FromMilliseconds(500));

        var output = await File.ReadAllBytesAsync(outputPath);
        var chunks = ReadChunks(output);
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
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("output.wav");
        await File.WriteAllBytesAsync(inputPath, CreateWave(1, 1, 2, 8, [0x70, 0x90]));

        var service = new DefaultWavAudioOffsetService();
        await service.OffsetAsync(inputPath, outputPath, TimeSpan.FromMilliseconds(500));

        var data = Assert.Single(ReadChunks(await File.ReadAllBytesAsync(outputPath)), chunk => chunk.Id == "data").Data;
        Assert.Equal(new byte[] { 0x80, 0x70, 0x90 }, data);
    }

    [Fact]
    public async Task OffsetAsync_MisalignedPcmData_ThrowsAndPreservesExistingTarget()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("invalid.wav");
        var outputPath = directory.File("existing.wav");
        var originalTarget = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        await File.WriteAllBytesAsync(inputPath, CreateWave(1, 1, 4, 16, [0x01, 0x02, 0x03]));
        await File.WriteAllBytesAsync(outputPath, originalTarget);

        var service = new DefaultWavAudioOffsetService();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.OffsetAsync(inputPath, outputPath, TimeSpan.FromSeconds(1)));

        Assert.Contains("BlockAlign", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalTarget, await File.ReadAllBytesAsync(outputPath));
        Assert.Empty(directory.FindTemporaryFilesFor(outputPath));
    }

    [Fact]
    public async Task OffsetAsync_UnsupportedCompressedFormat_ThrowsAndDoesNotCreateTarget()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("unsupported.wav");
        var outputPath = directory.File("output.wav");
        await File.WriteAllBytesAsync(inputPath, CreateWave(6, 1, 8_000, 8, [0x01, 0x02]));

        var service = new DefaultWavAudioOffsetService();
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.OffsetAsync(inputPath, outputPath, TimeSpan.Zero));

        Assert.Contains("0x0006", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
        Assert.Empty(directory.FindTemporaryFilesFor(outputPath));
    }

    [Fact]
    public async Task OffsetAsync_CommitFails_LeavesExistingTargetUntouchedAndRemovesCompletedTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("input.wav");
        var outputPath = directory.File("existing.wav");
        var sourceFrames = new byte[] { 0x11, 0x12, 0x21, 0x22 };
        var originalTarget = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        await File.WriteAllBytesAsync(inputPath, CreateWave(1, 1, 4, 16, sourceFrames));
        await File.WriteAllBytesAsync(outputPath, originalTarget);
        byte[]? completedTemporaryFile = null;

        var service = new DefaultWavAudioOffsetService((temporaryPath, destinationPath) =>
        {
            completedTemporaryFile = File.ReadAllBytes(temporaryPath);
            throw new IOException($"Simulated failure replacing {destinationPath}");
        });

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            service.OffsetAsync(inputPath, outputPath, TimeSpan.FromMilliseconds(250)));

        Assert.Contains("Simulated failure", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(completedTemporaryFile);
        Assert.Equal(
            new byte[] { 0x00, 0x00, 0x11, 0x12, 0x21, 0x22 },
            Assert.Single(ReadChunks(completedTemporaryFile), chunk => chunk.Id == "data").Data);
        Assert.Equal(originalTarget, await File.ReadAllBytesAsync(outputPath));
        Assert.Empty(directory.FindTemporaryFilesFor(outputPath));
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.WavOffsetTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);

        public string File(string fileName) => Path.Combine(RootPath, fileName);

        public string[] FindTemporaryFilesFor(string outputPath) => Directory.GetFiles(
            RootPath,
            $".{Path.GetFileName(outputPath)}.*.tmp",
            SearchOption.TopDirectoryOnly);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
