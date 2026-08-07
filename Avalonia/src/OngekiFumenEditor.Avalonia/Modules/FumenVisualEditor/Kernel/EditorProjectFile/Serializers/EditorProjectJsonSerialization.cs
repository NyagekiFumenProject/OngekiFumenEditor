using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;

internal static class EditorProjectJsonSerialization
{
    private static readonly EditorProjectJsonSourceGenerationContext context = CreateContext();

    public static JsonTypeInfo<T> GetTypeInfo<T>()
    {
        return (JsonTypeInfo<T>)(context.GetTypeInfo(typeof(T)) ??
            throw new NotSupportedException($"JSON metadata is unavailable for {typeof(T)}."));
    }

    private static EditorProjectJsonSourceGenerationContext CreateContext()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        options.Converters.Add(new TimeSpanJsonConverter());
        return new EditorProjectJsonSourceGenerationContext(options);
    }

    private sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var result = default(TimeSpan);
            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                if (reader.GetString() != "Ticks")
                    continue;
                if (!reader.Read())
                    throw new JsonException("Unable to read TimeSpan ticks.");

                result = TimeSpan.FromTicks(reader.GetInt64());
            }

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TimeSpan value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("Ticks", value.Ticks);
            writer.WriteEndObject();
        }
    }
}

[JsonSerializable(typeof(EditorProjectDataModel))]
[JsonSerializable(typeof(EditorProjectDataModel_V0_5_2))]
internal partial class EditorProjectJsonSourceGenerationContext : JsonSerializerContext;
