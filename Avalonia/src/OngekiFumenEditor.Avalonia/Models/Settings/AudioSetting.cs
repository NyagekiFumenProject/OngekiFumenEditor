using System.Text.Json.Serialization.Metadata;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class AudioSetting : SettingModelBase<AudioSetting>
{
    public static JsonTypeInfo<AudioSetting> JsonTypeInfo => JsonSourceGenerateContext.Default.AudioSetting;

    private static readonly Lazy<AudioSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static AudioSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<AudioSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial string SoundFolderPath { get; set; } = ".\\Resources\\sounds\\";

    [ObservableProperty]
    public partial int AudioOutputType { get; set; } = 1;

    [ObservableProperty]
    public partial int AudioSampleRate { get; set; } = 48000;

    [ObservableProperty]
    public partial float MusicVolume { get; set; } = 1f;

    [ObservableProperty]
    public partial float SoundVolume { get; set; } = 1f;

    [ObservableProperty]
    public partial bool EnableSoundMultiPlay { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableVarspeed { get; set; } = false;

    [ObservableProperty]
    public partial int VarspeedReadDurationMs { get; set; } = 50;
}
