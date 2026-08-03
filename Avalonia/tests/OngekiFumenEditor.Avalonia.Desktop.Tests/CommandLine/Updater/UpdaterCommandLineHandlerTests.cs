using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Updater;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Updater;

public sealed class UpdaterCommandLineHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    public async Task HandleAsync_PropagatesProgramUpdateResultAndWritesOnlyFailures(int serviceExitCode)
    {
        var message = serviceExitCode == 0 ? string.Empty : $"failure {serviceExitCode}";
        var service = new StubProgramUpdateService(new(serviceExitCode, message));
        var output = new RecordingOutput();
        var handler = new UpdaterCommandLineHandler(service, output);
        var options = new UpdaterOption();

        var exitCode = await handler.HandleAsync(options, CancellationToken.None);

        Assert.Equal(serviceExitCode, exitCode);
        Assert.Same(options, service.Options);
        if (serviceExitCode == 0)
        {
            Assert.Empty(output.Errors);
        }
        else
        {
            Assert.Equal(message, Assert.Single(output.Errors));
        }
    }

    private sealed class StubProgramUpdateService(ProgramUpdateResult result) : IProgramUpdateService
    {
        public UpdaterOption? Options { get; private set; }

        public Task<ProgramUpdateResult> UpdateAsync(
            UpdaterOption option,
            CancellationToken cancellationToken = default)
        {
            Options = option;
            return Task.FromResult(result);
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
}
