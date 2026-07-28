using MigratableSerializer.Wrapper;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Migrations
{
	public class Migration_V0_5_2_To_Latest : MigrationBase<EditorProjectDataModel_V0_5_2, EditorProjectDataModel>
	{
		public override bool CanDowngradable => false;

		public override Task<EditorProjectDataModel_V0_5_2> DowngradeAsync(EditorProjectDataModel toObj)
		{
			return Task.FromResult<EditorProjectDataModel_V0_5_2>(null);
		}

		public override async Task<EditorProjectDataModel> UpgradeAsync(EditorProjectDataModel_V0_5_2 fromObj)
		{
			var ms = new MemoryStream();
			await JsonSerializer.SerializeAsync(ms, fromObj);
			ms.Position = 0;
			var r = await JsonSerializer.DeserializeAsync<EditorProjectDataModel>(ms);
			return r;
		}
	}
}


