using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Avalonia.Parser;
using System.Text;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class SvgCommandLineHandlerTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    [InlineData("audio")]
    public async Task HandleAsync_AnyRelativeSvgPath_ReturnsMinusOneWithoutCallingDependencies(string relativePath)
    {
        var dependencies = new HandlerDependencies();
        var options = CreateAbsoluteOptions();
        if (relativePath == "input")
            options.InputFumenFilePath = "source.nyageki";
        else if (relativePath == "output")
            options.OutputFilePath = "preview.svg";
        else
            options.AudioFilePath = "music.wav";

        var exitCode = await dependencies.CreateHandler().HandleAsync(options, CancellationToken.None);

        Assert.Equal(SvgCommandLineHandler.RelativePathExitCode, exitCode);
        Assert.Equal(0, dependencies.ParserManager.InvocationCount);
        Assert.Equal(0, dependencies.AudioDurationProvider.InvocationCount);
        Assert.Equal(0, dependencies.PreviewSvgGenerator.InvocationCount);
        Assert.Equal(0, dependencies.SvgRasterizer.InvocationCount);
        Assert.Single(dependencies.Output.Errors);
    }

    [Fact]
    public async Task HandleAsync_ExistingAudio_UsesExactAudioDuration()
    {
        using var files = new TemporaryFiles();
        var expectedDuration = TimeSpan.FromMilliseconds(9876.5);
        var dependencies = new HandlerDependencies(expectedDuration);
        var options = files.CreateOptions(audioExists: true);

        var exitCode = await dependencies.CreateHandler().HandleAsync(options, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, dependencies.AudioDurationProvider.InvocationCount);
        Assert.Equal(expectedDuration, dependencies.PreviewSvgGenerator.Options?.Duration);
        Assert.Equal(expectedDuration, options.Duration);
        Assert.Empty(dependencies.Output.Errors);
    }

    [Fact]
    public async Task HandleAsync_MissingAudio_UsesChartTailPlusFiveGrids()
    {
        using var files = new TemporaryFiles();
        var fumen = CreateFumen(12);
        var dependencies = new HandlerDependencies(fumen: fumen);
        var options = files.CreateOptions(audioExists: false);
        var expectedDuration = TGridCalculator.ConvertTGridToAudioTime(
            new TGrid(17, 0),
            fumen.BpmList);

        var exitCode = await dependencies.CreateHandler().HandleAsync(options, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, dependencies.AudioDurationProvider.InvocationCount);
        Assert.Equal(expectedDuration, dependencies.PreviewSvgGenerator.Options?.Duration);
        Assert.Equal(expectedDuration, options.Duration);
        Assert.Empty(dependencies.Output.Errors);
    }

    [Theory]
    [InlineData("generator")]
    [InlineData("rasterizer")]
    public async Task HandleAsync_GeneratorOrRasterizerFailure_ReturnsMinusTwoAndWritesError(string failure)
    {
        using var files = new TemporaryFiles();
        var dependencies = new HandlerDependencies();
        var options = files.CreateOptions(audioExists: false);
        if (failure == "generator")
        {
            dependencies.PreviewSvgGenerator.Exception = new IOException("svg generator failed");
        }
        else
        {
            options.RenderAsPng = true;
            dependencies.SvgRasterizer.Exception = new IOException("png rasterizer failed");
        }

        var exitCode = await dependencies.CreateHandler().HandleAsync(options, CancellationToken.None);

        Assert.Equal(SvgCommandLineHandler.GenerationFailedExitCode, exitCode);
        Assert.Contains(failure, Assert.Single(dependencies.Output.Errors), StringComparison.OrdinalIgnoreCase);
    }

    private static SvgGenerateOption CreateAbsoluteOptions() => new()
    {
        InputFumenFilePath = Path.GetFullPath("source.nyageki"),
        OutputFilePath = Path.GetFullPath("preview.svg"),
        AudioFilePath = Path.GetFullPath("music.wav")
    };

    private static OngekiFumen CreateFumen(float tailUnit = 4)
    {
        var fumen = new OngekiFumen();
        fumen.Taps.Add(new Tap
        {
            TGrid = new TGrid(tailUnit, 0),
            XGrid = new XGrid(0, 0)
        });
        return fumen;
    }

    private sealed class HandlerDependencies
    {
        public RecordingParserManager ParserManager { get; }
        public RecordingPreviewSvgGenerator PreviewSvgGenerator { get; } = new();
        public RecordingAudioDurationProvider AudioDurationProvider { get; }
        public RecordingSvgRasterizer SvgRasterizer { get; } = new();
        public RecordingOutput Output { get; } = new();

        public HandlerDependencies(TimeSpan? audioDuration = null, OngekiFumen? fumen = null)
        {
            ParserManager = new RecordingParserManager(fumen ?? CreateFumen());
            AudioDurationProvider = new RecordingAudioDurationProvider(
                audioDuration ?? TimeSpan.FromSeconds(30));
        }

        public SvgCommandLineHandler CreateHandler() => new(
            ParserManager,
            PreviewSvgGenerator,
            AudioDurationProvider,
            SvgRasterizer,
            Output);
    }

    private sealed class RecordingParserManager(OngekiFumen fumen) : IFumenParserManager
    {
        public int InvocationCount { get; private set; }

        public IFumenSerializable GetSerializer(string saveFilePath) => null!;

        public IFumenDeserializable GetDeserializer(string loadFilePath)
        {
            InvocationCount++;
            return new StubDeserializer(fumen);
        }

        public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions() => [];
        public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions() => [];
    }

    private sealed class StubDeserializer(OngekiFumen fumen) : IFumenDeserializable
    {
        public string FileFormatName => "stub";
        public string[] SupportFumenFileExtensions => [".nyageki"];
        public Task<OngekiFumen> DeserializeAsync(Stream stream) => Task.FromResult(fumen);
    }

    private sealed class RecordingPreviewSvgGenerator : IPreviewSvgGenerator
    {
        public int InvocationCount { get; private set; }
        public SvgGenerateOption? Options { get; private set; }
        public Exception? Exception { get; set; }

        public Task<byte[]> GenerateSvgAsync(OngekiFumen fumen, SvgGenerateOption option)
        {
            InvocationCount++;
            Options = option;
            return Exception is null
                ? Task.FromResult(Encoding.UTF8.GetBytes(
                    "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"))
                : Task.FromException<byte[]>(Exception);
        }
    }

    private sealed class RecordingAudioDurationProvider(TimeSpan duration) : IAudioDurationProvider
    {
        public int InvocationCount { get; private set; }

        public Task<TimeSpan> GetDurationAsync(string audioFilePath, CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(duration);
        }
    }

    private sealed class RecordingSvgRasterizer : ISvgRasterizer
    {
        public int InvocationCount { get; private set; }
        public Exception? Exception { get; set; }

        public Task RasterizeAsync(
            ReadOnlyMemory<byte> svgData,
            string outputFilePath,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
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

    private sealed class TemporaryFiles : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.SvgHandlerTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryFiles() => Directory.CreateDirectory(RootPath);

        public SvgGenerateOption CreateOptions(bool audioExists)
        {
            var inputPath = Path.Combine(RootPath, "source.nyageki");
            var audioPath = Path.Combine(RootPath, "music.wav");
            File.WriteAllBytes(inputPath, []);
            if (audioExists)
                File.WriteAllBytes(audioPath, []);

            return new SvgGenerateOption
            {
                InputFumenFilePath = inputPath,
                OutputFilePath = Path.Combine(RootPath, "preview.svg"),
                AudioFilePath = audioPath
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
