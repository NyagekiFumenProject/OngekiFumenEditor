using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Convert;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.CommandLine.Convert;

public sealed class ConvertCommandLineHandlerTests
{
    [Fact]
    public async Task HandleAsync_OptionsWithRelativeVirtualPaths_ArePassedToService()
    {
        var service = new RecordingConvertService(new(true));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);
        var options = new FumenConvertOption
        {
            InputFumenFile = new StubSimpleFile("source.nyageki", "relative/source.nyageki"),
            OutputFumenFile = new StubSimpleFile("output.ogkr", "relative/output.ogkr")
        };

        var exitCode = await handler.HandleAsync(options, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(1, service.InvocationCount);
        Assert.Same(options, service.LastOption);
        Assert.Empty(output.Errors);
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

    [Fact]
    public async Task HandleAsync_CancellationRequested_RethrowsCancellationWithoutWritingError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new RecordingConvertService(
            new(true),
            new OperationCanceledException(cancellation.Token));
        var output = new RecordingOutput();
        var handler = new ConvertCommandLineHandler(service, output);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(CreateAbsoluteOptions(), cancellation.Token));

        Assert.Equal(1, service.InvocationCount);
        Assert.Empty(output.Errors);
    }

    private static FumenConvertOption CreateAbsoluteOptions() => new()
    {
        InputFumenFile = new StubSimpleFile("input.nyageki", Path.GetFullPath("input.nyageki")),
        OutputFumenFile = new StubSimpleFile("output.ogkr", Path.GetFullPath("output.ogkr"))
    };

    private sealed class RecordingConvertService(
        FumenConverterWrapper.GenerateResult result,
        Exception? exception = null) : IFumenConvertService
    {
        public int InvocationCount { get; private set; }

        public FumenConvertOption? LastOption { get; private set; }

        public Task<FumenConverterWrapper.GenerateResult> GenerateAsync(
            FumenConvertOption option,
            OngekiFumen? inMemoryFumen = null,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            LastOption = option;
            return exception is null
                ? Task.FromResult(result)
                : Task.FromException<FumenConverterWrapper.GenerateResult>(exception);
        }
    }

    private sealed class StubSimpleFile(string fileName, string fullPath) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => fullPath;
        public string FileName => fileName;
        public long FileLength => 0;

        public ValueTask<string[]> ReadAllLines() => ValueTask.FromResult(Array.Empty<string>());

        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Array.Empty<byte>());

        public Task<Stream> OpenRead() => Task.FromResult<Stream>(new MemoryStream());

        public Task<Stream> OpenWrite() => Task.FromResult<Stream>(new MemoryStream());

        public async Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var stream = new MemoryStream();
            await writer(stream, cancellationToken);
        }

        public void Dispose()
        {
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
