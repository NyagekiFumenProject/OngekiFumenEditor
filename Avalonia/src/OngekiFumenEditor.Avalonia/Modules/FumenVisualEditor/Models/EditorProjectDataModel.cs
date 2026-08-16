using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	// Pure persistable data class: only project-related settings and options live here.
	// Runtime state (parsed fumen, file handles, locators, recent-record identity) is owned
	// by the companion EditorContext.
	public class EditorProjectDataModel : EditorProjectDataModel_V0_5_2
	{
		public readonly static Version VERSION = new(0, 5, 4);
		public override Version Version => VERSION;
	}
}
