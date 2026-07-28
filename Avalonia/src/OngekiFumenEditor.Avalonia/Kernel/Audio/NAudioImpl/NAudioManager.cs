namespace OngekiFumenEditor.Avalonia.Kernel.Audio.NAudioImpl;

/// <summary>
/// NAudio backend placeholder. Implementation is intentionally not migrated.
/// </summary>
internal class NAudioManager : IAudioManager
{
    public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } =
    [
        (".mp3", "Audio File"),
        (".wav", "Audio File"),
        (".acb", "Criware Audio File")
    ];

    public float SoundVolume
    {
        get => 1;
        set { }
    }

    public float MusicVolume
    {
        get => 1;
        set { }
    }

    public float MusicSpeed
    {
        get => 1;
        set { }
    }

    public Task<IAudioPlayer> LoadAudioAsync(string filePath)
    {
        throw new NotSupportedException("NAudio backend is not migrated in Avalonia build.");
    }

    public Task<ISoundPlayer> LoadSoundAsync(string filePath)
    {
        throw new NotSupportedException("NAudio backend is not migrated in Avalonia build.");
    }

    public void Dispose()
    {
    }
}
