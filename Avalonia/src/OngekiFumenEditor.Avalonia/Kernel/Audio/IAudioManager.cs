using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public partial interface IAudioManager : IDisposable
{
    bool EnableVarspeed { get; }

    float SoundVolume { get; set; }
    float MusicVolume { get; set; }
    float MusicSpeed { get; set; }

    Task<ISoundPlayer> LoadSoundAsync(ISimpleFile file);
    Task<IAudioPlayer> LoadAudioAsync(ISimpleFile file);

    IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; }
}

