using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Convert;

public sealed class ConvertCommandLineDefinitionTests
{
    [Fact]
    public async Task Invoke_AllOptions_BindsStronglyTypedOptionsAndCallsInjectedHandler()
    {
        var handler = new RecordingHandler(27);
        var output = new RecordingOutput();
        var definition = new ConvertCommandLineDefinition(handler, output);
        var inputPath = Path.GetFullPath(Path.Combine("input charts", "source.nyageki"));
        var outputPath = Path.GetFullPath(Path.Combine("output charts", "result.ogkr"));

        var result = await InvokeAsync(
            definition,
            "convert",
            "--inputFile",
            inputPath,
            "--outputFile",
            outputPath,
            "--standardize");

        Assert.Equal(27, result.ExitCode);
        var options = Assert.IsType<FumenConvertOption>(handler.Options);
        var inputFile = Assert.IsType<LocalSimpleFile>(options.InputFumenFile);
        var outputFile = Assert.IsType<LocalSimpleFile>(options.OutputFumenFile);
        Assert.Equal(inputPath, inputFile.FullPath);
        Assert.Equal(Path.GetFileName(inputPath), inputFile.FileName);
        Assert.Equal(outputPath, outputFile.FullPath);
        Assert.Equal(Path.GetFileName(outputPath), outputFile.FileName);
        Assert.True(options.IsStandarizeFumen);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => inputFile.OpenRead());
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            ((ISimpleFile)outputFile).WriteAllBytesAsync(Array.Empty<byte>()));
        Assert.Empty(output.Errors);
        Assert.Equal(string.Empty, result.Error);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    public async Task Invoke_RelativePath_DoesNotCallHandler(string relativePath)
    {
        var handler = new RecordingHandler(0);
        var output = new RecordingOutput();
        var definition = new ConvertCommandLineDefinition(handler, output);
        var inputPath = relativePath == "input"
            ? "source.nyageki"
            : Path.GetFullPath("source.nyageki");
        var outputPath = relativePath == "output"
            ? "result.ogkr"
            : Path.GetFullPath("result.ogkr");

        var result = await InvokeAsync(
            definition,
            "convert",
            "--inputFile",
            inputPath,
            "--outputFile",
            outputPath);

        Assert.Equal(ConvertCommandLineDefinition.RelativePathExitCode, result.ExitCode);
        Assert.Null(handler.Options);
        Assert.Contains(Lang.CliArgumentNotAbsolutePath, Assert.Single(output.Errors), StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Error);
    }

    [Theory]
    [InlineData("--inputFile")]
    [InlineData("--outputFile")]
    public async Task Invoke_MissingRequiredOption_DoesNotCallHandler(string suppliedOption)
    {
        var handler = new RecordingHandler(0);
        var definition = new ConvertCommandLineDefinition(handler, new RecordingOutput());
        var value = Path.GetFullPath("chart.ogkr");

        var result = await InvokeAsync(definition, "convert", suppliedOption, value);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(handler.Options);
        Assert.Contains(
            suppliedOption == "--inputFile" ? "--outputFile" : "--inputFile",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_UnknownOption_DoesNotCallHandler()
    {
        var handler = new RecordingHandler(0);
        var definition = new ConvertCommandLineDefinition(handler, new RecordingOutput());

        var result = await InvokeAsync(
            definition,
            "convert",
            "--inputFile",
            Path.GetFullPath("input.nyageki"),
            "--outputFile",
            Path.GetFullPath("output.ogkr"),
            "--unexpected");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(handler.Options);
        Assert.Contains("--unexpected", result.Error, StringComparison.Ordinal);
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

    private sealed class RecordingHandler(int exitCode) : ICommandLineHandler<FumenConvertOption>
    {
        public FumenConvertOption? Options { get; private set; }

        public Task<int> HandleAsync(FumenConvertOption options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.FromResult(exitCode);
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

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
