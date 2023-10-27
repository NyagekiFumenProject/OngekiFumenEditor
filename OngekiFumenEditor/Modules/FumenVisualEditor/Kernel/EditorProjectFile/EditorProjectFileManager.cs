using MigratableSerializer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Migrations;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System.IO;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjManager
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
			AddSerializer(new EditorProjectDataModelSerializer_Latest());
			manager.AddMigration(new Migration_V0_5_2_To_Latest());
		}

		public Task<EditorProjectDataModel> Create()
		{
			return Task.FromResult(new EditorProjectDataModel());
		}

		public async Task<EditorProjectDataModel> Load(string filePath)
		{
			var buffer = await File.ReadAllBytesAsync(filePath);
			var editorProj = await manager.Load<EditorProjectDataModel>(buffer);

			return editorProj;
		}

		public async Task<EditorProjectDataModel> Clone(EditorProjectDataModel proj)
		{
			var ms = new MemoryStream();
			await manager.Save(ms, proj, typeof(EditorProjectDataModel));
			return await manager.Load<EditorProjectDataModel>(ms.ToArray());
		}

		public Task Save(string filePath, EditorProjectDataModel proj)
			=> Save<EditorProjectDataModel>(filePath, proj);

		public async Task Save<T>(string filePath, EditorProjectDataModel proj) where T : EditorProjectDataModelBase
		{
			using var fs = File.OpenWrite(filePath);
			await manager.Save(fs, proj, typeof(T));
		}
	}
}
