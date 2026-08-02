using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using SixLabors.ImageSharp;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class SvgCommandIntegrationTests
{
    [Fact]
    public async Task SvgFixture_NoAudio_ProducesWellFormedSvgWithDeclaredPositiveDimensions()
    {
        using var directory = new TemporaryDirectory();
        var output = new RecordingOutput();
        using var provider = CreateProvider(output: output);
        var outputPath = directory.File("preview.svg");

        var exitCode = await ExecuteSvgAsync(provider, outputPath, directory.File("missing.wav"));

        Assert.True(exitCode == 0, string.Join(Environment.NewLine, output.Errors));
        Assert.True(File.Exists(outputPath));
        var document = XDocument.Load(outputPath, LoadOptions.None);
        var root = Assert.IsType<XElement>(document.Root);
        Assert.Equal("svg", root.Name.LocalName);
        var dimensions = ReadDeclaredDimensions(root);
        Assert.True(dimensions.Width > 0);
        Assert.True(dimensions.Height > 0);
        Assert.NotNull(root.Element(root.Name.Namespace + "rect"));
    }

    [Fact]
    public async Task SvgFixture_ExistingAudio_UsesAudioDurationRatherThanChartTail()
    {
        using var directory = new TemporaryDirectory();
        var expectedDuration = TimeSpan.FromSeconds(73.25);
        var durationProvider = new RecordingAudioDurationProvider(expectedDuration);
        var previewGenerator = new RecordingPreviewSvgGenerator();
        using var provider = CreateProvider(durationProvider, previewGenerator);
        var audioPath = directory.File("music.wav");
        await File.WriteAllBytesAsync(audioPath, []);

        var exitCode = await ExecuteSvgAsync(provider, directory.File("preview.svg"), audioPath);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, durationProvider.InvocationCount);
        Assert.Equal(expectedDuration, previewGenerator.Duration);
    }

    [Fact]
    public async Task SvgFixture_Png_EndsAtIendAndImageSharpDecodesDeclaredDimensions()
    {
        using var directory = new TemporaryDirectory();
        var output = new RecordingOutput();
        using var provider = CreateProvider(output: output);
        var audioPath = directory.File("missing.wav");
        var svgPath = directory.File("preview.svg");
        var pngPath = directory.File("preview.png");

        var svgExitCode = await ExecuteSvgAsync(provider, svgPath, audioPath);
        var pngExitCode = await ExecuteSvgAsync(provider, pngPath, audioPath, png: true);

        Assert.True(svgExitCode == 0, string.Join(Environment.NewLine, output.Errors));
        Assert.True(pngExitCode == 0, string.Join(Environment.NewLine, output.Errors));
        var root = Assert.IsType<XElement>(XDocument.Load(svgPath).Root);
        var declared = ReadDeclaredDimensions(root);
        var expectedWidth = checked((int)Math.Ceiling(declared.Width));
        var expectedHeight = checked((int)Math.Ceiling(declared.Height));
        var pngData = await File.ReadAllBytesAsync(pngPath);
        var pngInfo = PngStructureAssertions.AssertValidPngEndingAtIend(pngData);
        Assert.Equal(expectedWidth, pngInfo.Width);
        Assert.Equal(expectedHeight, pngInfo.Height);

        using var decoded = Image.Load(pngData);
        Assert.Equal(expectedWidth, decoded.Width);
        Assert.Equal(expectedHeight, decoded.Height);
    }

    private static async Task<int> ExecuteSvgAsync(
        ServiceProvider provider,
        string outputPath,
        string audioPath,
        bool png = false)
    {
        var args = new List<string>
        {
            "svg",
            "--inputFile", Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki"),
            "--outputFile", outputPath,
            "--audioFile", audioPath
        };
        if (png)
            args.Add("--png");
        return await provider.GetRequiredService<ICommandExecutor>().ExecuteAsync(args.ToArray());
    }

    private static ServiceProvider CreateProvider(
        IAudioDurationProvider? durationProvider = null,
        IPreviewSvgGenerator? previewSvgGenerator = null,
        ICommandLineOutput? output = null)
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        if (durationProvider is not null)
            services.AddSingleton(durationProvider);
        if (previewSvgGenerator is not null)
            services.AddSingleton(previewSvgGenerator);
        if (output is not null)
            services.AddSingleton(output);
        return services.BuildServiceProvider();
    }

    private static (double Width, double Height) ReadDeclaredDimensions(XElement root)
    {
        var width = double.Parse(
            Assert.IsType<XAttribute>(root.Attribute("width")).Value,
            CultureInfo.InvariantCulture);
        var height = double.Parse(
            Assert.IsType<XAttribute>(root.Attribute("height")).Value,
            CultureInfo.InvariantCulture);
        return (width, height);
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

    private sealed class RecordingPreviewSvgGenerator : IPreviewSvgGenerator
    {
        public TimeSpan? Duration { get; private set; }

        public Task<byte[]> GenerateSvgAsync(OngekiFumen fumen, SvgGenerateOption option)
        {
            Duration = option.Duration;
            return Task.FromResult(Encoding.UTF8.GetBytes(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"/>"));
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.SvgIntegrationTests",
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
