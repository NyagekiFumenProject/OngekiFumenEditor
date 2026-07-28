using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.EditorProjectFile.Serializers
{
	public class EditorProjectDataModelSerializer_Latest : CommonEditorProjectFileSerializer<EditorProjectDataModel>
	{
		public override Version Version => EditorProjectDataModel.VERSION;
	}
}


