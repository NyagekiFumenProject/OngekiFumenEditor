using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

/// <summary>
/// Keeps color settings stable across the WPF object-shaped format and the Avalonia JSON format.
/// </summary>
public sealed class SystemDrawingColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var argb))
                    return Color.FromArgb(argb);
                break;

            case JsonTokenType.String:
                var text = reader.GetString();
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out argb))
                    return Color.FromArgb(argb);

                if (!string.IsNullOrWhiteSpace(text) &&
                    Enum.TryParse<KnownColor>(text, ignoreCase: true, out var knownColor))
                    return Color.FromKnownColor(knownColor);
                break;

            case JsonTokenType.StartObject:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    var objectValue = document.RootElement;
                    var red = ReadChannel(objectValue, "R");
                    var green = ReadChannel(objectValue, "G");
                    var blue = ReadChannel(objectValue, "B");
                    var alpha = ReadChannel(objectValue, "A", 255);
                    var isEmpty = ReadBoolean(objectValue, "IsEmpty");

                    if (isEmpty && red == 0 && green == 0 && blue == 0 && alpha == 0)
                        return Color.Empty;

                    return Color.FromArgb(alpha, red, green, blue);
                }
        }

        throw new JsonException($"The JSON value is not a valid {typeof(Color).FullName}.");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToArgb());
    }

    private static int ReadChannel(JsonElement value, string name, int fallback = 0)
    {
        if (!value.TryGetProperty(name, out var property))
            return fallback;

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var channel)
            ? Math.Clamp(channel, 0, 255)
            : fallback;
    }

    private static bool ReadBoolean(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.True;
    }
}
