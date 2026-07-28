namespace OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Sound;

public class SoundEvent
{
    public SoundControl Sounds { get; set; }
    public TimeSpan Time { get; set; }

    public override string ToString() => $"{Time} {Sounds}";
}

