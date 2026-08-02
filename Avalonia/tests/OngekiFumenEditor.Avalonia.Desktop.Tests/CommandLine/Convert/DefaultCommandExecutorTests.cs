using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine;

public sealed class DefaultCommandExecutorTests
{
    [Fact]
    public async Task RootHelp_ListsEveryRegisteredCommand()
    {
        var executor = new DefaultCommandExecutor(
        [
            new StubDefinition("alpha"),
            new StubDefinition("beta")
        ]);

        var result = await InvokeAsync(executor.RootCommand, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("alpha", result.Output, StringComparison.Ordinal);
        Assert.Contains("beta", result.Output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Constructor_DuplicateCommandNamesIgnoringCase_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new DefaultCommandExecutor(
        [
            new StubDefinition("convert"),
            new StubDefinition("CONVERT")
        ]));

        Assert.Contains("Duplicate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("CONVERT", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--verbose")]
    [InlineData("-v")]
    public async Task ExecuteAsync_VerbosityAliasAfterSubcommand_InvokesCommand(string alias)
    {
        var invocationCount = 0;
        var executor = new DefaultCommandExecutor(
        [
            new StubDefinition("sample", () => invocationCount++)
        ]);

        var exitCode = await executor.ExecuteAsync(["sample", alias]);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public async Task UnknownCommand_DoesNotInvokeAnyRegisteredCommand()
    {
        var invocationCount = 0;
        var executor = new DefaultCommandExecutor(
        [
            new StubDefinition("known", () => invocationCount++)
        ]);

        var result = await InvokeAsync(executor.RootCommand, "unknown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(0, invocationCount);
        Assert.Contains("unknown", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<InvocationResult> InvokeAsync(RootCommand rootCommand, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error
        };

        var exitCode = await rootCommand.Parse(args).InvokeAsync(configuration);
        return new InvocationResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed class StubDefinition(string name, Action? action = null) : ICommandLineDefinition
    {
        public Command CreateCommand()
        {
            var command = new Command(name);
            command.SetAction((ParseResult _) =>
            {
                action?.Invoke();
                return 0;
            });
            return command;
        }
    }

    private sealed record InvocationResult(int ExitCode, string Output, string Error);
}
