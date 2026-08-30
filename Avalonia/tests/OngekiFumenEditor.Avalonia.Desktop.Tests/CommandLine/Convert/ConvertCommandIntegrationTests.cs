using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Convert;

public sealed class ConvertCommandIntegrationTests
{
    [Fact]
    public async Task GenerateAsync_MissingInputFile_ReturnsNoFumenInput()
    {
        using var provider = CreateProvider();
        using var outputFile = new TestSimpleFile("converted.ogkr");

        var result = await provider.GetRequiredService<IFumenConvertService>().GenerateAsync(
            new FumenConvertOption { OutputFumenFile = outputFile });

        Assert.False(result.IsSuccess);
        Assert.Equal(Lang.NoFumenInput, result.Message);
        Assert.Equal(0, outputFile.WriteAsyncCount);
    }

    [Fact]
    public async Task GenerateAsync_MissingOutputFile_ReturnsOutputNotSelected()
    {
        using var provider = CreateProvider();

        var result = await provider.GetRequiredService<IFumenConvertService>().GenerateAsync(
            new FumenConvertOption(),
            new OngekiFumen());

        Assert.False(result.IsSuccess);
        Assert.Equal(Lang.OutputFumenFileNotSelect, result.Message);
    }

    [Fact]
    public void AddOngekiFumenEditorDesktopCommandLine_RegistersDefinitionHandlerAndExecutorAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(
            provider.GetServices<ICommandLineDefinition>().OfType<ConvertCommandLineDefinition>());
        var handler = provider.GetRequiredService<ICommandLineHandler<FumenConvertOption>>();
        var firstExecutor = provider.GetRequiredService<ICommandExecutor>();
        var secondExecutor = provider.GetRequiredService<ICommandExecutor>();

        Assert.IsType<ConvertCommandLineDefinition>(definition);
        Assert.IsType<ConvertCommandLineHandler>(handler);
        Assert.IsType<DefaultCommandExecutor>(firstExecutor);
        Assert.Same(firstExecutor, secondExecutor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConvertFixture_ToOgkr_ProducesReparseableChartWithPreservedContent(bool standardize)
    {
        using var directory = new TemporaryDirectory();
        using var provider = CreateProvider();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki");
        var outputPath = directory.File(standardize ? "standardized.ogkr" : "converted.ogkr");
        Assert.True(File.Exists(fixturePath), $"Built-in fixture was not copied to: {fixturePath}");
        var args = new List<string>
        {
            "convert",
            "--inputFile",
            fixturePath,
            "--outputFile",
            outputPath
        };
        if (standardize)
            args.Add("--standardize");

        var exitCode = await provider.GetRequiredService<ICommandExecutor>().ExecuteAsync(args.ToArray());

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 100);
        Assert.Empty(directory.FindTemporaryFiles());

        var parserManager = provider.GetRequiredService<IFumenParserManager>();
        var deserializer = Assert.IsAssignableFrom<IFumenDeserializable>(
            parserManager.GetDeserializer(outputPath));
        await using var stream = File.OpenRead(outputPath);
        var converted = await deserializer.DeserializeAsync(stream);
        Assert.Equal("1.7.0", converted.MetaInfo.Version.ToString());
        Assert.Equal("Avalonia migration test", converted.MetaInfo.Creator);
        Assert.Single(converted.Lanes);
        Assert.Single(converted.Taps);
        Assert.Single(converted.Taps, x => x.ReferenceLaneStart is not null);
        Assert.Single(converted.EnemySets);
    }

    [Fact]
    public async Task StandardizeFixture_Twice_ProducesIdenticalOutput()
    {
        using var directory = new TemporaryDirectory();
        using var provider = CreateProvider();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki");
        var firstOutputPath = directory.File("standardized-first.ogkr");
        var secondOutputPath = directory.File("standardized-second.ogkr");
        var service = provider.GetRequiredService<IFumenConvertService>();
        using var inputFile = new LocalSimpleFile(fixturePath);
        using var firstOutputFile = new LocalSimpleFile(firstOutputPath);
        using var secondOutputFile = new LocalSimpleFile(secondOutputPath);

        var firstResult = await service.GenerateAsync(new FumenConvertOption
        {
            InputFumenFile = inputFile,
            OutputFumenFile = firstOutputFile,
            IsStandarizeFumen = true
        });

        Assert.True(firstResult.IsSuccess, firstResult.Message);
        var parserManager = provider.GetRequiredService<IFumenParserManager>();
        var deserializer = Assert.IsAssignableFrom<IFumenDeserializable>(
            parserManager.GetDeserializer(firstOutputPath));
        await using var firstStream = File.OpenRead(firstOutputPath);
        var firstStandardizedFumen = await deserializer.DeserializeAsync(firstStream);

        var secondResult = await service.GenerateAsync(
            new FumenConvertOption
            {
                OutputFumenFile = secondOutputFile,
                IsStandarizeFumen = true
            },
            firstStandardizedFumen);

        Assert.True(secondResult.IsSuccess, secondResult.Message);
        Assert.Equal(
            await File.ReadAllBytesAsync(firstOutputPath),
            await File.ReadAllBytesAsync(secondOutputPath));
        Assert.Empty(directory.FindTemporaryFiles());
    }

    [Fact]
    public async Task GenerateAsync_NonLocalSimpleFiles_UsesTransactionalWriteAndRoundTrips()
    {
        Log.Initialize(new Log([]));
        using var provider = CreateProvider();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki");
        using var input = new TestSimpleFile(
            "minimal.nyageki",
            await File.ReadAllBytesAsync(fixturePath));
        using var output = new TestSimpleFile("converted.ogkr");
        var options = new FumenConvertOption
        {
            InputFumenFile = input,
            OutputFumenFile = output
        };

        var result = await provider.GetRequiredService<IFumenConvertService>().GenerateAsync(options);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("picker/minimal.nyageki", input.FullPath);
        Assert.Equal("picker/converted.ogkr", output.FullPath);
        Assert.Equal(1, input.OpenReadAsyncCount);
        Assert.Equal(1, output.WriteAsyncCount);
        Assert.True(output.Content.Length > 100);

        var parserManager = provider.GetRequiredService<IFumenParserManager>();
        var deserializer = Assert.IsAssignableFrom<IFumenDeserializable>(
            parserManager.GetDeserializer(output.FileName));
        await using var stream = await output.OpenRead();
        var converted = await deserializer.DeserializeAsync(stream);
        Assert.Equal("Avalonia migration test", converted.MetaInfo.Creator);
        Assert.Single(converted.Lanes);
        Assert.Single(converted.Taps);
    }

    [Fact]
    public async Task GenerateAsync_UsesOutputFileNameForConverterFormat()
    {
        var converter = new RecordingConverter();
        var service = new DefaultFumenConvertService(
            new UnusedParserManager(),
            converter,
            []);
        using var output = new TestSimpleFile("result.ogkr");

        var result = await service.GenerateAsync(
            new FumenConvertOption { OutputFumenFile = output },
            new OngekiFumen());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(output.FileName, converter.SavePathOrFormat);
        Assert.Equal(1, output.WriteAsyncCount);
    }

    [Fact]
    public async Task ConvertFixture_UnsupportedOutputFormat_ReturnsFailureWithoutCreatingOutput()
    {
        using var directory = new TemporaryDirectory();
        var output = new RecordingOutput();
        using var provider = CreateProvider(output);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki");
        var outputPath = directory.File("converted.unsupported");

        var exitCode = await provider.GetRequiredService<ICommandExecutor>().ExecuteAsync(
        [
            "convert",
            "--inputFile",
            fixturePath,
            "--outputFile",
            outputPath
        ]);

        Assert.Equal(ConvertCommandLineHandler.ConversionFailedExitCode, exitCode);
        Assert.False(File.Exists(outputPath));
        Assert.Empty(directory.FindTemporaryFiles());
        Assert.Contains("Conversion", Assert.Single(output.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_CancellationAfterConversion_PreservesExistingTargetAndRemovesTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var outputPath = directory.File("existing.ogkr");
        var original = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        await File.WriteAllBytesAsync(outputPath, original);
        using var cancellation = new CancellationTokenSource();
        Log.Initialize(new Log([]));
        var service = new DefaultFumenConvertService(
            new UnusedParserManager(),
            new CancelingConverter(cancellation),
            []);
        using var outputFile = new LocalSimpleFile(outputPath);
        var options = new FumenConvertOption
        {
            OutputFumenFile = outputFile
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GenerateAsync(options, new OngekiFumen(), cancellation.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(outputPath));
        Assert.Empty(directory.FindTemporaryFiles());
    }

    private static ServiceProvider CreateProvider(ICommandLineOutput? output = null)
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        if (output is not null)
            services.AddSingleton(output);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingOutput : ICommandLineOutput
    {
        public List<string> Errors { get; } = [];

        public Task WriteErrorLineAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class CancelingConverter(CancellationTokenSource cancellation) : IFumenConverter
    {
        public Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat)
        {
            cancellation.Cancel();
            return Task.FromResult<byte[]>([0x01, 0x02, 0x03]);
        }
    }

    private sealed class UnusedParserManager : IFumenParserManager
    {
        public IFumenSerializable GetSerializer(string saveFilePath) => null!;
        public IFumenDeserializable GetDeserializer(string loadFilePath) => null!;
        public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions() => [];
        public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions() => [];
    }

    private sealed class RecordingConverter : IFumenConverter
    {
        public string? SavePathOrFormat { get; private set; }

        public Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat)
        {
            SavePathOrFormat = savePathOrFormat;
            return Task.FromResult<byte[]>([0x01, 0x02]);
        }
    }

    private sealed class TestSimpleFile(string fileName, byte[]? initialContent = null) : ISimpleFile
    {
        private byte[] content = initialContent?.ToArray() ?? [];

        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"picker/{fileName}";
        public string FileName => fileName;
        public long FileLength => content.LongLength;
        public byte[] Content => content.ToArray();
        public int WriteAsyncCount { get; private set; }
        public int OpenReadAsyncCount { get; private set; }

        public ValueTask<string[]> ReadAllLines() => throw new NotSupportedException();
        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Content);
        public Task<Stream> OpenRead() =>
            Task.FromResult<Stream>(new MemoryStream(content, writable: false));

        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenReadAsyncCount++;
            return OpenRead();
        }

        public Task<Stream> OpenWrite() =>
            throw new InvalidOperationException("The conversion service must use WriteAsync.");

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            WriteAsyncCount++;
            await using var stream = new MemoryStream();
            await writer(stream, cancellationToken);
            content = stream.ToArray();
        }

        public void Dispose()
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.CommandLineTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);

        public string File(string fileName) => Path.Combine(RootPath, fileName);

        public string[] FindTemporaryFiles() =>
            Directory.GetFiles(RootPath, "*.tmp", SearchOption.TopDirectoryOnly);

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
