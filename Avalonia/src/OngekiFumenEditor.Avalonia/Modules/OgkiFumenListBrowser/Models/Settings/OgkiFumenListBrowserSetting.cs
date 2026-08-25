#nullable enable

using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Models.Settings;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models.Settings;

public partial class OgkiFumenListBrowserSetting : SettingModelBase<OgkiFumenListBrowserSetting>
{
    public static JsonTypeInfo<OgkiFumenListBrowserSetting> JsonTypeInfo =>
        OngekiJsonSourceGenerateContext.Default.OgkiFumenListBrowserSetting;

    private static readonly Lazy<OgkiFumenListBrowserSetting> defaultInstance =
        new(() => LoadDefault(JsonTypeInfo));

    public static OgkiFumenListBrowserSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<OgkiFumenListBrowserSetting> JsonTypeInfoCore => JsonTypeInfo;

    /// <summary>
    /// An opaque Avalonia storage bookmark. Raw local paths are intentionally never persisted.
    /// </summary>
    [ObservableProperty]
    public partial string RootFolderBookmark { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RootFolderDisplayName { get; set; } = string.Empty;
}
