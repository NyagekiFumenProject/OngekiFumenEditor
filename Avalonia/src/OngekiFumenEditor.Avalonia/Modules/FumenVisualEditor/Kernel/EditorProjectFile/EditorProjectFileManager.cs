using MigratableSerializer;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Migrations;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile
{
	public class EditorProjectFileManager
	{
		private MigratableSerializerManager manager;

		public EditorProjectFileManager()
		{
			manager = new MigratableSerializerManager();
			void AddSerializer<T>(CommonEditorProjectFileSerializer<T> serializer) where T : EditorProjectDataModelBase
			{
				manager.AddFormatter(serializer);
				manager.AddParser(serializer);
			}

			AddSerializer(new EditorProjectDataModelSerializer_V0_5_2());
			AddSerializer(new EditorProjectDataModelSerializer_V0_5_4());
			AddSerializer(new EditorProjectDataModelSerializer_Latest());
			manager.AddMigration(new Migration_V0_5_2_To_Latest());
			manager.AddMigration(new Migration_V0_5_4_To_Latest());
		}

		public Task<EditorProjectDataModel> Create()
		{
			return Task.FromResult(new EditorProjectDataModel());
		}

		public async Task<EditorProjectDataModel> Load(string filePath, CancellationToken cancellationToken = default)
		{
			var buffer = await File.ReadAllBytesAsync(filePath, cancellationToken);
			var editorProj = await manager.Load<EditorProjectDataModel>(buffer);

			return editorProj;
		}

		public async Task<EditorProjectDataModel> Load(
			Stream stream,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(stream);
			using var buffer = new MemoryStream();
			await stream.CopyToAsync(buffer, cancellationToken);
			return await manager.Load<EditorProjectDataModel>(buffer.ToArray());
		}

		public async Task<EditorProjectDataModel> Load(
			ITemporaryFile file,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(file);
			await using var stream = await file.OpenReadAsync(cancellationToken);
			return await Load(stream, cancellationToken);
		}

		public async Task<EditorProjectDataModel> Clone(EditorProjectDataModel proj)
		{
			var ms = new MemoryStream();
			await manager.Save(ms, proj, typeof(EditorProjectDataModel));
			return await manager.Load<EditorProjectDataModel>(ms.ToArray());
		}

		public Task Save(
			string filePath,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default)
			=> Save<EditorProjectDataModel>(filePath, proj, cancellationToken);

		public async Task Save<T>(
			string filePath,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default) where T : EditorProjectDataModelBase
		{
			await using var fs = new FileStream(
				filePath,
				FileMode.Create,
				FileAccess.Write,
				FileShare.None,
				81_920,
				FileOptions.Asynchronous | FileOptions.SequentialScan);
			await Save<T>(fs, proj, cancellationToken);
		}

		public Task Save(
			Stream stream,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default)
			=> Save<EditorProjectDataModel>(stream, proj, cancellationToken);

		public async Task Save<T>(
			Stream stream,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default) where T : EditorProjectDataModelBase
		{
			ArgumentNullException.ThrowIfNull(stream);
			ArgumentNullException.ThrowIfNull(proj);
			cancellationToken.ThrowIfCancellationRequested();
			await manager.Save(stream, proj, typeof(T));
		}

		public Task Save(
			ITemporaryFile file,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default)
			=> Save<EditorProjectDataModel>(file, proj, cancellationToken);

		public Task Save<T>(
			ITemporaryFile file,
			EditorProjectDataModel proj,
			CancellationToken cancellationToken = default) where T : EditorProjectDataModelBase
		{
			ArgumentNullException.ThrowIfNull(file);
			return file.WriteAsync(
				(stream, writerCancellationToken) =>
					Save<T>(stream, proj, writerCancellationToken),
				cancellationToken);
		}
	}
}


