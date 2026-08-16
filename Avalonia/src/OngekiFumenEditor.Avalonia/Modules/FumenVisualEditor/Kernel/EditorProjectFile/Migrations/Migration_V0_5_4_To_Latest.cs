using MigratableSerializer.Wrapper;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Migrations
{
	public class Migration_V0_5_4_To_Latest : MigrationBase<EditorProjectDataModel_V0_5_4, EditorProjectDataModel>
	{
		public override bool CanDowngradable => false;

		public override Task<EditorProjectDataModel_V0_5_4> DowngradeAsync(EditorProjectDataModel toObj)
		{
			return Task.FromResult<EditorProjectDataModel_V0_5_4>(null);
		}

		public override async Task<EditorProjectDataModel> UpgradeAsync(EditorProjectDataModel_V0_5_4 fromObj)
		{
			var ms = new MemoryStream();
			await JsonSerializer.SerializeAsync(
				ms,
				fromObj,
				EditorProjectJsonSerialization.GetTypeInfo<EditorProjectDataModel_V0_5_4>());
			ms.Position = 0;
			return await JsonSerializer.DeserializeAsync(
				ms,
				EditorProjectJsonSerialization.GetTypeInfo<EditorProjectDataModel>()) ??
				throw new JsonException("Unable to migrate the editor project to the latest version.");
		}
	}
}
