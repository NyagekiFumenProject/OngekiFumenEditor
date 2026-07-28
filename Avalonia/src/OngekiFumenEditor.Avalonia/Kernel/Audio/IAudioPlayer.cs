using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public interface IAudioPlayer : IDisposable, INotifyPropertyChanged
{
    TimeSpan CurrentTime { get; }
    float Speed { get; set; }
    TimeSpan Duration { get; }
    bool IsPlaying { get; }
    bool IsAvaliable { get; }

    void Play();
    void Stop();
    void Pause();
    void Seek(TimeSpan timeSpan, bool pause);

    public delegate void OnPlaybackFinishedFunc();
    public event OnPlaybackFinishedFunc OnPlaybackFinished;

    Task<SampleData> GetSamplesAsync();
}

