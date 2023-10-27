using MigratableSerializer.Wrapper;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public abstract class CommonEditorProjectFileSerializer<T> : SerializerBase<T> where T : EditorProjectDataModelBase
	{
		public abstract Version Version { get; }

		public static readonly JsonSerializerOptions jsonSerializerOptions;
		private class TimeSpanJsonConverter : JsonConverter<TimeSpan>
		{
			public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				var result = default(TimeSpan);
				if (reader.TokenType == JsonTokenType.StartObject)
				{
					while (reader.Read())
					{
						if (reader.TokenType == JsonTokenType.EndObject)
							break;
						if (reader.GetString() == "Ticks")
						{
							if (!reader.Read())
								throw new Exception("Json parse TimeSpan rrror");
							var ticks = reader.GetInt64();
							result = TimeSpan.FromTicks(ticks);
						}
					}
				}
				return result;
			}

			public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
			{
				writer.WriteStartObject();
				writer.WriteNumber("Ticks", value.Ticks);
				writer.WriteEndObject();
			}
		}

		static CommonEditorProjectFileSerializer()
		{
			jsonSerializerOptions = new JsonSerializerOptions()
			{
				WriteIndented = true
			};
			jsonSerializerOptions.Converters.Add(new TimeSpanJsonConverter());
		}

		public override async Task<bool> CheckParsableAsync(byte[] buffer)
		{
			ValueTask<TX> DeserializeAnonymousType<TX>(Stream ms, TX _) => JsonSerializer.DeserializeAsync<TX>(ms, jsonSerializerOptions);

			var ms = new MemoryStream(buffer);
			var r = await DeserializeAnonymousType(ms, new { Version = new Version() });

			return r?.Version == Version;
		}

		public override async Task<T> ParseAsync(byte[] buffer)
		{
			var ms = new MemoryStream(buffer);
			return await JsonSerializer.DeserializeAsync<T>(ms, jsonSerializerOptions);
		}

		public override Task WriteAsync(Stream stream, T obj)
		{
			return JsonSerializer.SerializeAsync(stream, obj, jsonSerializerOptions);
		}
	}
}
