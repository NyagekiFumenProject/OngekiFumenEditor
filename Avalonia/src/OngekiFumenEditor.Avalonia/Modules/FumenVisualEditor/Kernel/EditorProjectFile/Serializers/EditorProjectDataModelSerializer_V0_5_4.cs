using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public class EditorProjectDataModelSerializer_V0_5_4 : CommonEditorProjectFileSerializer<EditorProjectDataModel_V0_5_4>
	{
		public override Version Version => EditorProjectDataModel_V0_5_4.VERSION;
	}
}
