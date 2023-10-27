using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public class EditorProjectDataModelSerializer_V0_5_2 : CommonEditorProjectFileSerializer<EditorProjectDataModel_V0_5_2>
	{
		public override Version Version => EditorProjectDataModel_V0_5_2.VERSION;
	}
}
