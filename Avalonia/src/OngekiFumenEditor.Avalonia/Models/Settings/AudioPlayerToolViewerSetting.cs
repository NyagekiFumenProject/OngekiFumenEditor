using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class AudioPlayerToolViewerSetting : SettingModelBase<AudioPlayerToolViewerSetting>
{
    public static JsonTypeInfo<AudioPlayerToolViewerSetting> JsonTypeInfo =>
        JsonSourceGenerateContext.Default.AudioPlayerToolViewerSetting;

    private static readonly Lazy<AudioPlayerToolViewerSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static AudioPlayerToolViewerSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<AudioPlayerToolViewerSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial int ResampleSize { get; set; } = 0;

    [ObservableProperty]
    public partial float WaveformVecticalScale { get; set; } = 0.7f;

    [ObservableProperty]
    public partial float DurationMsPerPixel { get; set; } = 10f;

    [ObservableProperty]
    public partial float CurrentTimeXOffset { get; set; } = 30f;

    [ObservableProperty]
    public partial bool EnableWaveformDisplay { get; set; } = true;

    [ObservableProperty]
    public partial int LimitFPS { get; set; } = -1;
}
