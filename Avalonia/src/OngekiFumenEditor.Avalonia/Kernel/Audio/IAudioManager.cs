namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public partial interface IAudioManager : IDisposable
{
    bool EnableVarspeed { get; }

    float SoundVolume { get; set; }
    float MusicVolume { get; set; }
    float MusicSpeed { get; set; }

    Task<ISoundPlayer> LoadSoundAsync(Stream stream);
    Task<IAudioPlayer> LoadAudioAsync(Stream audioFileStream);
    Task<IAudioPlayer> LoadAudioAsync(Stream acbStream, Stream externalAwbStream);

    IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; }
}

