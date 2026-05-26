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

		public override Task<bool> CheckParsableAsync(byte[] buffer)
		{
			// 原版反序列化整个文件只为读 Version;改用 Utf8JsonReader 流式扫到第一个
			// "Version" 顶层属性即可返回,常见情况几十字节就能判定,远早于完整解析。
			return Task.FromResult(TryReadTopLevelVersion(buffer, out var ver) && ver == Version);
		}

		private static bool TryReadTopLevelVersion(ReadOnlySpan<byte> buffer, out Version version)
		{
			version = null;
			var reader = new Utf8JsonReader(buffer, isFinalBlock: true, state: default);
			if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
				return false;

			var depth = reader.CurrentDepth;
			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == depth)
					return false;

				if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1)
					continue;

				if (!reader.ValueTextEquals("Version"u8))
				{
					reader.Skip();
					continue;
				}

				if (!reader.Read())
					return false;
				var raw = reader.GetString();
				return Version.TryParse(raw, out version);
			}
			return false;
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
