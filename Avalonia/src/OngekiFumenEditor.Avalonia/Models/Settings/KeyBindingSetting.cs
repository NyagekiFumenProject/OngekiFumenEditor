using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class KeyBindingSetting : SettingModelBase<KeyBindingSetting>
{
    public static JsonTypeInfo<KeyBindingSetting> JsonTypeInfo => JsonSourceGenerateContext.Default.KeyBindingSetting;

    private static readonly Lazy<KeyBindingSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static KeyBindingSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<KeyBindingSetting> JsonTypeInfoCore => JsonTypeInfo;
}
