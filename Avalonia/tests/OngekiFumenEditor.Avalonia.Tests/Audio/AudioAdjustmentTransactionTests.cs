using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow;
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

    private sealed class StubWavAudioOffsetService(Action operation) : IWavAudioOffsetService
    {
        public Task OffsetAsync(
            string inputWavFilePath,
            string outputWavFilePath,
            TimeSpan offset,
            CancellationToken cancellationToken = default)
        {
            operation();
            return Task.CompletedTask;
        }
    }
}
