using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Convert;

public sealed class ConvertCommandLineHandlerTests
{
    [Fact]
    public async Task HandleAsync_RelativePath_ReturnsLegacyPathExitCodeWithoutCallingService()
    {
        var service = new RecordingConvertService(new(true));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(new FumenConvertOption
        {
            InputFumenFilePath = "relative.nyageki",
            OutputFumenFilePath = Path.GetFullPath("output.ogkr")
        }, CancellationToken.None);

        Assert.Equal(ConvertCommandLineHandler.RelativePathExitCode, exitCode);
        Assert.Equal(0, service.InvocationCount);
        Assert.Single(output.Errors);
        Assert.Contains("Relative", output.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ServiceFailure_ReturnsLegacyConversionExitCodeAndWritesError()
    {
        var service = new RecordingConvertService(new(false, "unsupported output format"));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(ConvertCommandLineHandler.ConversionFailedExitCode, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Single(output.Errors);
        Assert.Contains("unsupported output format", output.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ServiceThrows_ReturnsLegacyConversionExitCodeAndWritesExceptionMessage()
    {
        var service = new RecordingConvertService(new(true), new IOException("target is read-only"));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(ConvertCommandLineHandler.ConversionFailedExitCode, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Contains("target is read-only", Assert.Single(output.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ServiceSuccess_ReturnsZeroWithoutWritingError()
    {
        var service = new RecordingConvertService(new(true));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);

        var exitCode = await handler.HandleAsync(CreateAbsoluteOptions(), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Empty(output.Errors);
    }

    private static FumenConvertOption CreateAbsoluteOptions() => new()
    {
        InputFumenFilePath = Path.GetFullPath("input.nyageki"),
        OutputFumenFilePath = Path.GetFullPath("output.ogkr")
    };

    private sealed class RecordingConvertService(
        FumenConverterWrapper.GenerateResult result,
        Exception? exception = null) : IFumenConvertService
    {
        public int InvocationCount { get; private set; }

        public Task<FumenConverterWrapper.GenerateResult> GenerateAsync(
            FumenConvertOption option,
            OngekiFumen? inMemoryFumen = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return exception is null
                ? Task.FromResult(result)
                : Task.FromException<FumenConverterWrapper.GenerateResult>(exception);
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
