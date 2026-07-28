namespace OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Sound;

public class DurationSoundEvent : SoundEvent
{
    public int LoopId { get; set; }
    public TimeSpan EndTime { get; set; }

    public override string ToString() => $"{base.ToString()} {LoopId} {EndTime}";
}

