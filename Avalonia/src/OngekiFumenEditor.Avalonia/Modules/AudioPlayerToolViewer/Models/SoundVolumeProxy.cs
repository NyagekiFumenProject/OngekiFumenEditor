using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.Audio;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Models;

public partial class SoundVolumeProxy : ObservableObject
{
    private readonly IFumenSoundPlayer soundPlayer;
    private readonly SoundControl sound;

    public string Name => sound.ToString();

    public float Volume
    {
        get => soundPlayer.GetVolume(sound) ?? 0;
        set
        {
            soundPlayer.SetVolume(sound, value);
            OnPropertyChanged(nameof(Volume));
        }
    }

    public bool IsValid => soundPlayer.GetVolume(sound) is not null;

    public SoundVolumeProxy(IFumenSoundPlayer soundPlayer, SoundControl sound)
    {
        this.soundPlayer = soundPlayer;
        this.sound = sound;
    }
}
