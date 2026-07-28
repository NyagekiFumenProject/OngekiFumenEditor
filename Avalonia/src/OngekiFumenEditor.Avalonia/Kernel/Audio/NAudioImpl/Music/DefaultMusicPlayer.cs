using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl.Music;

/// <summary>
/// NAudio music player placeholder. Implementation is intentionally not migrated.
/// </summary>
internal class DefaultMusicPlayer : IAudioPlayer
{
    public TimeSpan CurrentTime => TimeSpan.Zero;
    public float Speed { get; set; } = 1;
    public TimeSpan Duration => TimeSpan.Zero;
    public bool IsPlaying => false;
    public bool IsAvaliable => false;

    public event IAudioPlayer.OnPlaybackFinishedFunc OnPlaybackFinished;
    public event PropertyChangedEventHandler PropertyChanged;

    public void Play()
    {
        throw new NotSupportedException("NAudio backend is not migrated in Avalonia build.");
    }

    public void Stop()
    {
    }

    public void Pause()
    {
    }

    public void Seek(TimeSpan timeSpan, bool pause)
    {
    }

    public Task<SampleData> GetSamplesAsync()
    {
        return Task.FromResult<SampleData>(default);
    }

    public void Dispose()
    {
    }
}
