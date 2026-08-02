using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class ConvertCommandIntegrationTests
{
    [Fact]
    public void AddOngekiFumenEditorDesktopCommandLine_RegistersDefinitionHandlerAndExecutorAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(provider.GetServices<ICommandLineDefinition>());
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
        var options = new FumenConvertOption
        {
            InputFumenFilePath = directory.File("unused.nyageki"),
            OutputFumenFilePath = outputPath
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
