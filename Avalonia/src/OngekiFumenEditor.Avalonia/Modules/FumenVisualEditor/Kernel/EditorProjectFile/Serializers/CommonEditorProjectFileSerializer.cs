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
			using var document = JsonDocument.Parse(buffer);
			var isMatch = document.RootElement.TryGetProperty(
				nameof(EditorProjectDataModelBase.Version),
				out var versionElement) &&
				versionElement.ValueKind == JsonValueKind.String &&
				System.Version.TryParse(versionElement.GetString(), out var version) &&
				version.Equals(Version);

			return Task.FromResult(isMatch);
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


