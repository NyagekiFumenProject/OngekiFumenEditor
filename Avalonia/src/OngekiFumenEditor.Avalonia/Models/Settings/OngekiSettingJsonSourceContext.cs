using System.Text.Json.Serialization;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models.Settings;

namespace Gekimini.Avalonia.Utils;

[JsonSerializable(typeof(ProgramSetting))]
[JsonSerializable(typeof(AudioSetting))]
[JsonSerializable(typeof(AudioPlayerToolViewerSetting))]
[JsonSerializable(typeof(DefaultWaveformSettings))]
[JsonSerializable(typeof(EditorGlobalSetting))]
[JsonSerializable(typeof(LogSetting))]
[JsonSerializable(typeof(KeyBindingSetting))]
[JsonSerializable(typeof(OgkiFumenListBrowserSetting))]
public partial class OngekiJsonSourceGenerateContext : JsonSerializerContext
{
}
