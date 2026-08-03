using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Jacket;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Jacket;

public sealed class JacketCommandLineHandlerTests
{
    [Theory]
    [InlineData("input")]
    [InlineData("output")]
    public async Task HandleAsync_AnyRelativeJacketPath_ReturnsMinusFiveWithoutCallingService(string relativePath)
    {
        var service = new StubJacketGenerateService(new(true));
        var output = new RecordingOutput();
        var handler = new JacketCommandLineHandler(service, output);
        var options = CreateAbsoluteOptions();
        if (relativePath == "input")
            options.InputImageFilePath = "jacket.png";
        else
            options.OutputAssetbundleFolderPath = "output";

        var exitCode = await handler.HandleAsync(options, CancellationToken.None);

        Assert.Equal(JacketCommandLineHandler.RelativePathExitCode, exitCode);
        Assert.Equal(0, service.InvocationCount);
        Assert.Single(output.Errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_ServiceFailureOrException_ReturnsMinusSixAndWritesReason(bool throws)
    {
        var reason = throws ? "native encoder missing" : "invalid jacket image";
        var service = new StubJacketGenerateService(
            new JacketGenerateResult(false, reason),
            throws ? new InvalidOperationException(reason) : null);
        var output = new RecordingOutput();
        var handler = new JacketCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(JacketCommandLineHandler.GenerationFailedExitCode, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Contains(reason, Assert.Single(output.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ServiceSuccess_ReturnsZeroWithoutError()
    {
        var service = new StubJacketGenerateService(new(true));
        var output = new RecordingOutput();
        var handler = new JacketCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Empty(output.Errors);
    }

    private static JacketGenerateOption CreateAbsoluteOptions() => new()
    {
        MusicId = 666,
        InputImageFilePath = Path.GetFullPath("jacket.png"),
        OutputAssetbundleFolderPath = Path.GetFullPath("output")
    };

    private sealed class StubJacketGenerateService(
        JacketGenerateResult result,
        Exception? exception = null) : IJacketGenerateService
    {
        public int InvocationCount { get; private set; }

        public Task<JacketGenerateResult> GenerateAsync(
            JacketGenerateOption option,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return exception is null
                ? Task.FromResult(result)
                : Task.FromException<JacketGenerateResult>(exception);
        }

        public Task<JacketImageData> GetMainImageDataAsync(
            byte[] abFileData,
            string filePath,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
