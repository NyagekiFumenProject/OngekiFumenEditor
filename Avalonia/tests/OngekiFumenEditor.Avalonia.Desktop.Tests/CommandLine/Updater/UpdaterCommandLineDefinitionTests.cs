using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;
using System.CommandLine;
using System.CommandLine.Invocation;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class UpdaterCommandLineDefinitionTests
{
    [Fact]
    public async Task Invoke_AllRequiredUpdaterOptions_BindsAndCallsInjectedHandler()
    {
        var handler = new RecordingHandler(-17);
        var definition = new UpdaterCommandLineDefinition(handler);
        var sourceFolder = Path.GetFullPath("source");
        var targetFolder = Path.GetFullPath("target");

        var result = await InvokeAsync(
            definition,
            "updater",
            "--sourceFolder", sourceFolder,
            "--targetFolder", targetFolder,
            "--sourceVersion", "1.2.3.4");

        Assert.Equal(-17, result.ExitCode);
        var options = Assert.IsType<UpdaterOption>(handler.Options);
        Assert.Equal(sourceFolder, options.SourceFolder);
        Assert.Equal(targetFolder, options.TargetFolder);
        Assert.Equal("1.2.3.4", options.SourceVersion);
        Assert.Equal(string.Empty, result.Error);
    }

    [Theory]
    [InlineData("--sourceFolder")]
    [InlineData("--targetFolder")]
    [InlineData("--sourceVersion")]
    public async Task Invoke_MissingEachRequiredUpdaterOption_DoesNotCallHandler(string missingOption)
    {
        var handler = new RecordingHandler(0);
        var definition = new UpdaterCommandLineDefinition(handler);
        var args = new List<string>
        {
            "updater",
            "--sourceFolder", Path.GetFullPath("source"),
            "--targetFolder", Path.GetFullPath("target"),
            "--sourceVersion", "1.2.3.4"
        };
        var optionIndex = args.IndexOf(missingOption);
        args.RemoveRange(optionIndex, 2);

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

    private sealed class RecordingHandler(int exitCode) : ICommandLineHandler<UpdaterOption>
    {
        public UpdaterOption? Options { get; private set; }

        public Task<int> HandleAsync(UpdaterOption options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.FromResult(exitCode);
        }
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
