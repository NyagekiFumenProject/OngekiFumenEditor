using System.Buffers.Binary;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class AcbGenerateServiceIntegrationTests
{
    [Fact]
    public async Task Generate_48KhzPcmWave_CreatesAcbAwbAndMusicSourceThatReopen()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("source.wav");
        WritePcmWave(inputPath, sampleRate: 48_000, duration: TimeSpan.FromMilliseconds(250));
        var temporaryRootPath = Path.Combine(directory.RootPath, "temp");
        var service = CreateService(temporaryRootPath);
        var options = new AcbGenerateOption
        {
            MusicId = 427,
            InputAudioFilePath = inputPath,
            OutputFolderPath = directory.RootPath,
            PreviewBeginTime = 60_000,
            PreviewEndTime = 80_000
        };

        var result = await service.GenerateAsync(options);

        Assert.True(result.IsSuccess, result.Message);
        var acbPath = AssertNonEmptyFile(directory.File("music0427.acb"));
        var awbPath = AssertNonEmptyFile(directory.File("music0427.awb"));
        var xmlPath = AssertNonEmptyFile(directory.File("MusicSource.xml"));

        AssertPreviewTime(acbPath, [0x03, 0xE7, 0x04], options.PreviewBeginTime);
        AssertPreviewTime(acbPath, [0x07, 0xD1, 0x04], options.PreviewEndTime);

        var document = XDocument.Load(xmlPath);
        var root = Assert.IsType<XElement>(document.Root);
        Assert.Equal("musicsource0427", root.Element("dataName")?.Value);
        Assert.Equal("0427", root.Element("Name")?.Element("id")?.Value);
        Assert.Equal("0427", root.Element("Name")?.Element("str")?.Value);
        Assert.Equal("music0427.acb", root.Element("acbFile")?.Element("path")?.Value);
        Assert.Equal("music0427.awb", root.Element("awbFile")?.Element("path")?.Value);

        AssertAcbCanBeReopened(acbPath, awbPath);
        AssertAwbCanBeReopened(awbPath);
        Assert.NotEmpty(Directory.EnumerateFiles(temporaryRootPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AcbConverter_ExternalAwb_UsesSimpleFilesAndWritesPlayableWav()
    {
        using var directory = new TemporaryDirectory();
        var inputPath = directory.File("source.wav");
        WritePcmWave(inputPath, sampleRate: 48_000, duration: TimeSpan.FromMilliseconds(250));
        var service = CreateService(Path.Combine(directory.RootPath, "temp"));
        var result = await service.GenerateAsync(new AcbGenerateOption
        {
            MusicId = 428,
            InputAudioFilePath = inputPath,
            OutputFolderPath = directory.RootPath,
            PreviewBeginTime = 60_000,
            PreviewEndTime = 80_000
        });

        Assert.True(result.IsSuccess, result.Message);
        using var acbFile = new LocalSimpleFile(directory.File("music0428.acb"));
        using var awbFile = new LocalSimpleFile(directory.File("music0428.awb"));
        using var outputFile = new LocalSimpleFile(directory.File("decoded.wav"));

        await using var acbStream = await acbFile.OpenRead();
        await using var awbStream = await awbFile.OpenRead();
        await AcbConverter.ConvertAcbFileToWavAsync(
            acbStream,
            awbStream,
            outputFile);

        Assert.True(new FileInfo(outputFile.FullPath).Length > 44);
        using var reader = new WaveFileReader(outputFile.FullPath);
        Assert.True(reader.TotalTime > TimeSpan.Zero);

        using var missingAwbOutputFile = new LocalSimpleFile(directory.File("decoded-missing-awb.wav"));
        await using var missingAwbAcbStream = await acbFile.OpenRead();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcbConverter.ConvertAcbFileToWavAsync(
                missingAwbAcbStream,
                null,
                missingAwbOutputFile));
    }

    private static void AssertPreviewTime(string acbPath, byte[] marker, int expectedMilliseconds)
    {
        var bytes = File.ReadAllBytes(acbPath);
        var markerIndex = bytes.AsSpan().IndexOf(marker);
        Assert.True(markerIndex >= 0, $"ACB preview marker {System.Convert.ToHexString(marker)} was not found.");
        var timeOffset = markerIndex + marker.Length;
        Assert.True(timeOffset + sizeof(uint) <= bytes.Length);
        Assert.Equal(
            checked((uint)expectedMilliseconds),
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(timeOffset, sizeof(uint))));
    }

    private static DefaultAcbGenerateService CreateService(string temporaryRootPath)
    {
        Log.Initialize(new Log([]));
        return new DefaultAcbGenerateService(new TestLocalTemporaryFolderProvider(temporaryRootPath));
    }

    private static void AssertAcbCanBeReopened(string acbPath, string awbPath)
    {
        var assembly = LoadAcbParserAssembly();
        var acbType = GetRequiredType(assembly, "DereTore.Exchange.Archive.ACB.AcbFile");
        var fromStream = Assert.IsAssignableFrom<MethodInfo>(acbType.GetMethod(
            "FromStream",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(FileStream)],
            modifiers: null));
        using var acbStream = File.Open(acbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var acb = Assert.IsAssignableFrom<IDisposable>(fromStream.Invoke(null, [acbStream]));

        Assert.True(GetRequiredProperty<uint>(acbType, acb, "FormatVersion") > 0);
        Assert.NotEmpty(GetRequiredProperty<Array>(acbType, acb, "Cues").Cast<object>());

        var externalAwb = GetRequiredProperty<object>(acbType, acb, "ExternalAwb");
        var afs2Type = externalAwb.GetType();
        Assert.Equal(
            Path.GetFullPath(awbPath),
            Path.GetFullPath(GetRequiredProperty<string>(afs2Type, externalAwb, "FileName")));
        Assert.NotEmpty(GetRequiredProperty<IDictionary>(afs2Type, externalAwb, "Files").Values.Cast<object>());

        var getFileNames = Assert.IsAssignableFrom<MethodInfo>(acbType.GetMethod(
            "GetFileNames",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null));
        var cueFileNames = Assert.IsType<string[]>(getFileNames.Invoke(acb, null));
        Assert.Equal(
            new[] { "music.hca", "preview.hca" },
            cueFileNames.OrderBy(static fileName => fileName, StringComparer.Ordinal));
        var openDataStream = Assert.IsAssignableFrom<MethodInfo>(acbType.GetMethod(
            "OpenDataStream",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string)],
            modifiers: null));
        foreach (var cueFileName in cueFileNames)
        {
            using var cueData = Assert.IsAssignableFrom<Stream>(openDataStream.Invoke(acb, [cueFileName]));
            Assert.True(
                cueData.Length > 16,
                $"Cue data '{cueFileName}' was unexpectedly small: {cueData.Length} bytes.");
        }
    }

    private static void AssertAwbCanBeReopened(string awbPath)
    {
        var assembly = LoadAcbParserAssembly();
        var afs2Type = GetRequiredType(assembly, "DereTore.Exchange.Archive.ACB.Afs2Archive");
        var constructor = Assert.IsAssignableFrom<ConstructorInfo>(afs2Type.GetConstructor(
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(Stream), typeof(long), typeof(string), typeof(bool)],
            modifiers: null));

        using var awbStream = File.Open(awbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var awb = Assert.IsAssignableFrom<IDisposable>(
            constructor.Invoke([awbStream, 0L, awbPath, false]));
        Assert.IsAssignableFrom<MethodInfo>(afs2Type.GetMethod("Initialize", Type.EmptyTypes)).Invoke(awb, null);

        Assert.True(GetRequiredProperty<uint>(afs2Type, awb, "Version") > 0);
        Assert.True(GetRequiredProperty<uint>(afs2Type, awb, "ByteAlignment") > 0);
        var record = Assert.Single(
            GetRequiredProperty<IDictionary>(afs2Type, awb, "Files").Values.Cast<object>());
        var recordType = record.GetType();
        var fileLength = GetRequiredProperty<long>(recordType, record, "FileLength");
        var fileOffset = GetRequiredProperty<long>(recordType, record, "FileOffsetAligned");
        Assert.True(fileLength > 16);
        Assert.InRange(fileOffset, 0, awbStream.Length - 1);
        Assert.True(fileOffset + fileLength <= awbStream.Length);
    }

    private static Assembly LoadAcbParserAssembly()
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "DereTore.Exchange.Archive.ACB.dll");
        Assert.True(File.Exists(assemblyPath), $"ACB parser assembly was not copied to test output: {assemblyPath}");
        return Assembly.LoadFrom(assemblyPath);
    }

    private static Type GetRequiredType(Assembly assembly, string typeName) =>
        Assert.IsAssignableFrom<Type>(assembly.GetType(typeName, throwOnError: false));

    private static T GetRequiredProperty<T>(Type ownerType, object instance, string propertyName)
    {
        var property = Assert.IsAssignableFrom<PropertyInfo>(ownerType.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance));
        return Assert.IsAssignableFrom<T>(property.GetValue(instance));
    }

    private static string AssertNonEmptyFile(string filePath)
    {
        Assert.True(File.Exists(filePath), $"Expected generated file: {filePath}");
        Assert.True(new FileInfo(filePath).Length > 0, $"Generated file was empty: {filePath}");
        return filePath;
    }

    private static void WritePcmWave(string filePath, int sampleRate, TimeSpan duration)
    {
        const short channels = 2;
        const short bitsPerSample = 16;
        var frameCount = checked((int)Math.Round(sampleRate * duration.TotalSeconds));
        var blockAlign = checked((short)(channels * bitsPerSample / 8));
        var byteRate = checked(sampleRate * blockAlign);
        var dataSize = checked(frameCount * blockAlign);

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sample = checked((short)(Math.Sin(2 * Math.PI * 440 * frame / sampleRate) * short.MaxValue * 0.25));
            writer.Write(sample);
            writer.Write(sample);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.AcbIntegrationTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);
        public string File(string fileName) => Path.Combine(RootPath, fileName);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
