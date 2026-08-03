using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class AcbCommandLineDefinitionTests
{
    [Fact]
    public async Task Invoke_DefaultOptions_UsesLegacyPreviewRange()
    {
        var handler = new RecordingHandler(0);
        var definition = new AcbCommandLineDefinition(handler);

        var result = await InvokeAsync(definition, CreateRequiredArguments());

        Assert.Equal(0, result.ExitCode);
        var options = Assert.IsType<AcbGenerateOption>(handler.Options);
        Assert.Equal(427, options.MusicId);
        Assert.Equal(Path.GetFullPath("input.wav"), options.InputAudioFilePath);
        Assert.Equal(Path.GetFullPath("output"), options.OutputFolderPath);
        Assert.Equal(60_000, options.PreviewBeginTime);
        Assert.Equal(80_000, options.PreviewEndTime);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public async Task Invoke_AllOptions_BindsAcbOptionsAndReturnsHandlerExitCode()
    {
        var handler = new RecordingHandler(29);
        var definition = new AcbCommandLineDefinition(handler);
        var args = CreateRequiredArguments().Concat(
        [
            "--previewBegin", "12345",
            "--previewEnd", "54321"
        ]).ToArray();

        var result = await InvokeAsync(definition, args);

        Assert.Equal(29, result.ExitCode);
        var options = Assert.IsType<AcbGenerateOption>(handler.Options);
        Assert.Equal(12_345, options.PreviewBeginTime);
        Assert.Equal(54_321, options.PreviewEndTime);
        Assert.Equal(string.Empty, result.Error);
    }

    [Theory]
    [InlineData("--musicId")]
    [InlineData("--inputFile")]
    [InlineData("--outputFolder")]
    public async Task Invoke_MissingEachRequiredAcbOption_DoesNotCallHandler(string missingOption)
    {
        var handler = new RecordingHandler(0);
        var definition = new AcbCommandLineDefinition(handler);
        var args = CreateRequiredArguments().ToList();
        var optionIndex = args.IndexOf(missingOption);
        args.RemoveRange(optionIndex, 2);

        var result = await InvokeAsync(definition, args.ToArray());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Null(handler.Options);
        Assert.Contains(missingOption, result.Error, StringComparison.Ordinal);
    }

    private static string[] CreateRequiredArguments() =>
    [
        "acb",
        "--musicId", "427",
        "--inputFile", Path.GetFullPath("input.wav"),
        "--outputFolder", Path.GetFullPath("output")
    ];

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

    private sealed class RecordingHandler(int exitCode) : ICommandLineHandler<AcbGenerateOption>
    {
        public AcbGenerateOption? Options { get; private set; }

        public Task<int> HandleAsync(AcbGenerateOption options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.FromResult(exitCode);
        }
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
