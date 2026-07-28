using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class DefaultWaveformSettings : SettingModelBase<DefaultWaveformSettings>
{
    public static JsonTypeInfo<DefaultWaveformSettings> JsonTypeInfo =>
        JsonSourceGenerateContext.Default.DefaultWaveformSettings;

    private static readonly Lazy<DefaultWaveformSettings> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static DefaultWaveformSettings Default => defaultInstance.Value;

    protected override JsonTypeInfo<DefaultWaveformSettings> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial bool ShowObjectPlaceLine { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowWaveform { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowTimingLine { get; set; } = true;
}
