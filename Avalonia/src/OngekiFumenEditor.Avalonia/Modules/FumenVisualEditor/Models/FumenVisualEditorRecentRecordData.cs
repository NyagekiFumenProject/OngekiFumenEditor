#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

internal sealed class FumenVisualEditorRecentRecordData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string FolderBookmark { get; set; } = string.Empty;

    public string ProjectFileLocator { get; set; } = string.Empty;

    public static byte[] Serialize(FumenVisualEditorRecentRecordData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return JsonSerializer.SerializeToUtf8Bytes(
            data,
            FumenVisualEditorRecentRecordJsonContext.Default.FumenVisualEditorRecentRecordData);
    }

    public static bool TryDeserialize(byte[]? bytes, out FumenVisualEditorRecentRecordData? data)
    {
        data = null;
        if (bytes is not { Length: > 0 })
            return false;

        try
        {
            data = JsonSerializer.Deserialize(
                bytes,
                FumenVisualEditorRecentRecordJsonContext.Default.FumenVisualEditorRecentRecordData);
            return data is
            {
                Version: CurrentVersion,
                FolderBookmark.Length: > 0,
                ProjectFileLocator.Length: > 0
            };
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}

[JsonSerializable(typeof(FumenVisualEditorRecentRecordData))]
internal partial class FumenVisualEditorRecentRecordJsonContext : JsonSerializerContext;
