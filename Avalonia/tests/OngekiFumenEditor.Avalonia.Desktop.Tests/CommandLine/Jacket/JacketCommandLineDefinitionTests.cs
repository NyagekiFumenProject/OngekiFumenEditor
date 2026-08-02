using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class JacketCommandLineDefinitionTests
{
    [Fact]
    public async Task Invoke_DefaultOptions_Uses520And220JacketDimensions()
    {
        var handler = new RecordingHandler(0);
        var definition = new JacketCommandLineDefinition(handler);

        var result = await InvokeAsync(definition, CreateRequiredArguments());

        Assert.Equal(0, result.ExitCode);
        var options = Assert.IsType<JacketGenerateOption>(handler.Options);
        Assert.Equal(520, options.Width);
        Assert.Equal(520, options.Height);
        Assert.Equal(220, options.WidthSmall);
        Assert.Equal(220, options.HeightSmall);
        Assert.True(options.UpdateAssetBytesFile);
    }

    [Fact]
    public async Task Invoke_DistinctSmallWidthAndHeight_BindsToCorrectProperties()
    {
        var handler = new RecordingHandler(23);
        var definition = new JacketCommandLineDefinition(handler);
        var args = CreateRequiredArguments().Concat(
        [
            "--outputWidth", "640",
            "--outputHeight", "480",
            "--outputWidthSmall", "321",
            "--outputHeightSmall", "123",
            "--updateAssetBytesFile", "false"
        ]).ToArray();

        var result = await InvokeAsync(definition, args);

        Assert.Equal(23, result.ExitCode);
        var options = Assert.IsType<JacketGenerateOption>(handler.Options);
        Assert.Equal(640, options.Width);
        Assert.Equal(480, options.Height);
        Assert.Equal(321, options.WidthSmall);
        Assert.Equal(123, options.HeightSmall);
        Assert.False(options.UpdateAssetBytesFile);
        Assert.Equal(string.Empty, result.Error);
    }

    [Theory]
    [InlineData("--musicId")]
    [InlineData("--outputFolder")]
    [InlineData("--inputFile")]
    public async Task Invoke_MissingEachRequiredJacketOption_DoesNotCallHandler(string missingOption)
    {
        var handler = new RecordingHandler(0);
        var definition = new JacketCommandLineDefinition(handler);
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
        "jacket",
        "--musicId", "666",
        "--outputFolder", Path.GetFullPath("output"),
        "--inputFile", Path.GetFullPath("jacket.png")
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

    private sealed class RecordingHandler(int exitCode) : ICommandLineHandler<JacketGenerateOption>
    {
        public JacketGenerateOption? Options { get; private set; }

        public Task<int> HandleAsync(JacketGenerateOption options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.FromResult(exitCode);
        }
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
