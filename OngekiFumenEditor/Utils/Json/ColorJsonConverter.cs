using System;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Utils.Json
{
    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var str = reader.GetString();

            if (string.IsNullOrEmpty(str))
                return Color.Empty;

            // #RRGGBB
            // #AARRGGBB
            if (str.StartsWith("#"))
            {
                return ColorTranslator.FromHtml(str);
            }

            // R,G,B
            // R,G,B,A
            if (str.Contains(","))
            {
                var parts = str.Split(',');

                if (parts.Length == 3)
                {
                    return Color.FromArgb(
                        int.Parse(parts[0]),
                        int.Parse(parts[1]),
                        int.Parse(parts[2]));
                }

                if (parts.Length == 4)
                {
                    return Color.FromArgb(
                        int.Parse(parts[3]), // A
                        int.Parse(parts[0]), // R
                        int.Parse(parts[1]), // G
                        int.Parse(parts[2])); // B
                }
            }

            // KnownColor / Name
            return Color.FromName(str);
        }

        public override void Write(Utf8JsonWriter writer,
            Color value,
            JsonSerializerOptions options)
        {
            if (value.IsNamedColor)
            {
                writer.WriteStringValue(value.Name);
                return;
            }

            // 输出 #AARRGGBB
            writer.WriteStringValue(
                $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }
}
