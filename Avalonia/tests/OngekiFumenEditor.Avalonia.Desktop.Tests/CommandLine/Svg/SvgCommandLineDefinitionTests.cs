using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Svg;

public sealed class SvgCommandLineDefinitionTests
{
    [Fact]
    public async Task Invoke_AllOptions_BindsSvgOptionsAndCallsInjectedHandler()
    {
        var handler = new RecordingHandler(19);
        var definition = new SvgCommandLineDefinition(handler);
        var inputPath = Path.GetFullPath("source.nyageki");
        var outputPath = Path.GetFullPath("preview.png");
        var audioPath = Path.GetFullPath("music.wav");

        var result = await InvokeAsync(
            definition,
            "svg",
            "--inputFile", inputPath,
            "--outputFile", outputPath,
            "--audioFile", audioPath,
            "--maxXGrid", "52.5",
            "--viewWidth", "1024",
            "--verticalScale", "1.75",
            "--soflanMode", "AbsSoflan",
            "--png");

        Assert.Equal(19, result.ExitCode);
        var options = Assert.IsType<SvgGenerateOption>(handler.Options);
        Assert.Equal(inputPath, options.InputFumenFilePath);
        Assert.Equal(outputPath, options.OutputFilePath);
        Assert.Equal(audioPath, options.AudioFilePath);
        Assert.Equal(52.5, options.XGridDisplayMaxUnit);
        Assert.Equal(1024, options.ViewWidth);
        Assert.Equal(1.75, options.VerticalScale);
        Assert.Equal(SoflanMode.AbsSoflan, options.SoflanMode);
        Assert.True(options.RenderAsPng);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task Invoke_DefaultOptions_UsesLegacySvgDefaults()
    {
        var handler = new RecordingHandler(0);
        var definition = new SvgCommandLineDefinition(handler);

        var result = await InvokeAsync(
            definition,
            "svg",
            "--inputFile", Path.GetFullPath("source.nyageki"),
            "--outputFile", Path.GetFullPath("preview.svg"),
            "--audioFile", Path.GetFullPath("missing.wav"));

        Assert.Equal(0, result.ExitCode);
        var options = Assert.IsType<SvgGenerateOption>(handler.Options);
        Assert.Equal(40, options.XGridDisplayMaxUnit);
        Assert.Equal(800, options.ViewWidth);
        Assert.Equal(1, options.VerticalScale);
        Assert.Equal(SoflanMode.Soflan, options.SoflanMode);
        Assert.False(options.RenderAsPng);
    }

    [Theory]
    [InlineData("--inputFile")]
    [InlineData("--outputFile")]
    [InlineData("--audioFile")]
    public async Task Invoke_MissingEachRequiredSvgPath_DoesNotCallHandler(string missingOption)
    {
        var handler = new RecordingHandler(0);
        var definition = new SvgCommandLineDefinition(handler);
        var args = new List<string> { "svg" };
        foreach (var option in new[] { "--inputFile", "--outputFile", "--audioFile" })
        {
            if (option != missingOption)
            {
                args.Add(option);
                args.Add(Path.GetFullPath($"{option[2..]}.dat"));
            }
        }

        var result = await InvokeAsync(definition, args.ToArray());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(handler.Options);
        Assert.Contains(missingOption, result.Error, StringComparison.Ordinal);
    }

    private static async Task<InvocationResult> InvokeAsync(
        ICommandLineDefinition definition,
        params string[] args)
    {
        var root = new RootCommand();
        root.Subcommands.Add(definition.CreateCommand());
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error
        };

        var exitCode = await root.Parse(args).InvokeAsync(configuration);
        return new InvocationResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed class RecordingHandler(int exitCode) : ICommandLineHandler<SvgGenerateOption>
    {
        public SvgGenerateOption? Options { get; private set; }

        public Task<int> HandleAsync(SvgGenerateOption options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.FromResult(exitCode);
        }
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
