using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Acb;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Acb;

public sealed class AcbCommandLineHandlerTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    public async Task HandleAsync_AnyRelativeAcbPath_ReturnsMinusSevenWithoutCallingService(
        string relativePath)
    {
        var service = new StubAcbGenerateService(new(true));
        var output = new RecordingOutput();
        var handler = new AcbCommandLineHandler(service, output);
        var options = CreateAbsoluteOptions();
        if (relativePath == "input")
            options.InputAudioFilePath = "input.wav";
        else
            options.OutputFolderPath = "output";

        var exitCode = await handler.HandleAsync(options, CancellationToken.None);

        Assert.Equal(AcbCommandLineHandler.RelativePathExitCode, exitCode);
        Assert.Equal(-7, exitCode);
        Assert.Equal(0, service.InvocationCount);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(output.Errors)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_ServiceFailureOrException_ReturnsMinusEightAndWritesReason(bool throws)
    {
        var reason = throws ? "ACB encoder unavailable" : "unsupported wave data";
        var service = new StubAcbGenerateService(
            new AcbGenerateResult(false, reason),
            throws ? new InvalidOperationException(reason) : null);
        var output = new RecordingOutput();
        var handler = new AcbCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(AcbCommandLineHandler.GenerationFailedExitCode, exitCode);
        Assert.Equal(-8, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Contains(reason, Assert.Single(output.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_AbsolutePaths_ForwardsSameOptionsAndCancellationTokenToInjectedService()
    {
        var service = new StubAcbGenerateService(new(true));
        var output = new RecordingOutput();
        var handler = new AcbCommandLineHandler(service, output);
        var options = CreateAbsoluteOptions();
        using var cancellation = new CancellationTokenSource();

        var exitCode = await handler.HandleAsync(options, cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Same(options, service.Options);
        Assert.Equal(cancellation.Token, service.CancellationToken);
        Assert.Empty(output.Errors);
    }

    [Fact]
    public async Task HandleAsync_ServiceCancellation_PropagatesWithoutWritingFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new StubAcbGenerateService(
            new(false),
            new OperationCanceledException(cancellation.Token));
        var output = new RecordingOutput();
        var handler = new AcbCommandLineHandler(service, output);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.HandleAsync(CreateAbsoluteOptions(), cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, service.InvocationCount);
        Assert.Empty(output.Errors);
    }

    private static AcbGenerateOption CreateAbsoluteOptions() => new()
    {
        MusicId = 427,
        InputAudioFilePath = Path.GetFullPath("input.wav"),
        OutputFolderPath = Path.GetFullPath("output"),
        PreviewBeginTime = 60_000,
        PreviewEndTime = 80_000
    };

    private sealed class StubAcbGenerateService(
        AcbGenerateResult result,
        Exception? exception = null) : IAcbGenerateService
    {
        public int InvocationCount { get; private set; }
        public AcbGenerateOption? Options { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<AcbGenerateResult> GenerateAsync(
            AcbGenerateOption option,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            Options = option;
            CancellationToken = cancellationToken;
            return exception is null
                ? Task.FromResult(result)
                : Task.FromException<AcbGenerateResult>(exception);
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
