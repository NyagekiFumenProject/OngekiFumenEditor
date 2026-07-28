using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class OptionGeneratorToolsSetting : SettingModelBase<OptionGeneratorToolsSetting>
{
    public static JsonTypeInfo<OptionGeneratorToolsSetting> JsonTypeInfo =>
        JsonSourceGenerateContext.Default.OptionGeneratorToolsSetting;

    private static readonly Lazy<OptionGeneratorToolsSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static OptionGeneratorToolsSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<OptionGeneratorToolsSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial string LastLoadedGameFolder { get; set; } = string.Empty;
}
