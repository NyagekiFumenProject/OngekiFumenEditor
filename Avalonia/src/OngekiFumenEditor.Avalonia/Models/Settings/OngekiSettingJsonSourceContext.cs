using System.Text.Json.Serialization;
using OngekiFumenEditor.Avalonia.Models.Settings;

namespace Gekimini.Avalonia.Utils;

[JsonSerializable(typeof(ProgramSetting))]
[JsonSerializable(typeof(AudioSetting))]
[JsonSerializable(typeof(AudioPlayerToolViewerSetting))]
[JsonSerializable(typeof(DefaultWaveformSettings))]
[JsonSerializable(typeof(EditorGlobalSetting))]
[JsonSerializable(typeof(LogSetting))]
[JsonSerializable(typeof(KeyBindingSetting))]
public partial class JsonSourceGenerateContext : JsonSerializerContext
{
}
