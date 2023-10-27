using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models
{
	public class EditorProjectDataModel : EditorProjectDataModel_V0_5_2
	{
		public readonly static Version VERSION = new(0, 5, 4);
		public override Version Version => VERSION;
	}
}
