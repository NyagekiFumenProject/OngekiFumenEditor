using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class AudioAdjustmentTransactionTests
{
    [Fact]
    public async Task ExecuteAsync_AudioSucceeds_RunsUndoCommitAfterAudioCompletion()
    {
        var events = new List<string>();
        var service = new StubWavAudioOffsetService(() => events.Add("audio-complete"));

        var result = await AudioAdjustmentTransaction.ExecuteAsync(
            service,
            "input.wav",
            "output.wav",
            TimeSpan.FromSeconds(1),
            () => events.Add("undo-commit"));

        Assert.True(result.isSuccess);
        Assert.Equal(string.Empty, result.msg);
        Assert.Equal(new[] { "audio-complete", "undo-commit" }, events);
    }

    [Fact]
    public async Task ExecuteAsync_AudioFails_DoesNotRunUndoCommit()
    {
        var undoCommitCount = 0;
        var service = new StubWavAudioOffsetService(
            () => throw new InvalidDataException("invalid wave"));

        var result = await AudioAdjustmentTransaction.ExecuteAsync(
            service,
            "input.wav",
            "output.wav",
            TimeSpan.FromSeconds(-1),
            () => undoCommitCount++);

        Assert.False(result.isSuccess);
        Assert.Equal("invalid wave", result.msg);
        Assert.Equal(0, undoCommitCount);
    }

    [Fact]
    public async Task ExecuteAsync_SimpleFiles_AudioSucceeds_RunsUndoCommitAfterStorageWrite()
    {
        var events = new List<string>();
        var service = new StubWavAudioOffsetService(() => events.Add("storage-write-complete"));
        using var input = new StubSimpleFile("input.wav");
        using var output = new StubSimpleFile("output.wav");

        var result = await AudioAdjustmentTransaction.ExecuteAsync(
            service,
            input,
            output,
            TimeSpan.FromMilliseconds(250),
            () => events.Add("undo-commit"));

        Assert.True(result.isSuccess);
        Assert.Equal("simple-files", service.LastOverload);
        Assert.Equal(new[] { "storage-write-complete", "undo-commit" }, events);
    }

    private sealed class StubWavAudioOffsetService(Action operation) : IWavAudioOffsetService
    {
        public string? LastOverload { get; private set; }

        public Task OffsetAsync(
            ISimpleFile inputWavFile,
            ISimpleFile outputWavFile,
            TimeSpan offset,
            CancellationToken cancellationToken = default)
        {
            LastOverload = "simple-files";
            operation();
            return Task.CompletedTask;
        }

        public Task OffsetAsync(
            string inputWavFilePath,
            ISimpleFile outputWavFile,
            TimeSpan offset,
            CancellationToken cancellationToken = default)
        {
            LastOverload = "path-to-simple-file";
            operation();
            return Task.CompletedTask;
        }

        public Task OffsetAsync(
            string inputWavFilePath,
            string outputWavFilePath,
            TimeSpan offset,
            CancellationToken cancellationToken = default)
        {
            LastOverload = "paths";
            operation();
            return Task.CompletedTask;
        }
    }

    private sealed class StubSimpleFile(string fileName) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => fileName;
        public string? LocalPath => null;
        public string FileName => fileName;
        public long FileLength => 0;

        public ValueTask<string[]> ReadAllLines() => throw new NotSupportedException();
        public ValueTask<byte[]> ReadAllBytes() => throw new NotSupportedException();
        public Task<Stream> OpenRead() => throw new NotSupportedException();
        public Task<Stream> OpenWrite() => throw new NotSupportedException();
        public Task WriteAsync(
            Func<Stream, CancellationToken, Task> writer,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }
}
