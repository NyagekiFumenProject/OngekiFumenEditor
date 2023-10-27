using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public class EditorProjectDataModelSerializer_Latest : CommonEditorProjectFileSerializer<EditorProjectDataModel>
	{
		public override Version Version => EditorProjectDataModel.VERSION;
	}
}
