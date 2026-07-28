using OngekiFumenEditor.Avalonia.Kernel.Audio;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Utils;

public class AudioPlayerAnimationClock
{
    private readonly IAudioPlayer audioPlayer;

    public AudioPlayerAnimationClock(IAudioPlayer audioPlayer)
    {
        this.audioPlayer = audioPlayer;
    }

    public TimeSpan CurrentTime => audioPlayer.CurrentTime;
}
