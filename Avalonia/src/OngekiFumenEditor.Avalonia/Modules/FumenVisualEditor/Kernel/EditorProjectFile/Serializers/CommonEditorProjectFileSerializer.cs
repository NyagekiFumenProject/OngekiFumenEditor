using MigratableSerializer.Wrapper;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public abstract class CommonEditorProjectFileSerializer<T> : SerializerBase<T> where T : EditorProjectDataModelBase
	{
		public abstract Version Version { get; }

		private static readonly JsonTypeInfo<T> jsonTypeInfo =
			EditorProjectJsonSerialization.GetTypeInfo<T>();

        public override Task<bool> CheckParsableAsync(byte[] buffer)
        {
            return Task.FromResult(TryReadTopLevelVersion(buffer, out var version) && version == Version);
        }

        private static bool TryReadTopLevelVersion(ReadOnlySpan<byte> buffer, out Version version)
        {
            version = null!;
            try
            {
                var reader = new Utf8JsonReader(buffer, isFinalBlock: true, state: default);
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                    return false;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
                        return false;

                    if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                        continue;

                    if (!reader.ValueTextEquals("Version"u8))
                    {
                        reader.Skip();
                        continue;
                    }

                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                        return false;
                    return Version.TryParse(reader.GetString(), out version);
                }
            }
            catch (JsonException)
            {
            }

            return false;
        }

		public override async Task<T> ParseAsync(byte[] buffer)
		{
			using var ms = new MemoryStream(buffer);
			return await JsonSerializer.DeserializeAsync(ms, jsonTypeInfo) ??
				throw new JsonException($"Unable to deserialize {typeof(T)}.");
		}

		public override Task WriteAsync(Stream stream, T obj)
		{
			return JsonSerializer.SerializeAsync(stream, obj, jsonTypeInfo);
		}
	}
}


