using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models
{
	// Pure persistable data class: only project-related settings and options live here.
	// Runtime state (parsed fumen, file handles and recent-record identity) is owned
	// by the companion EditorContext.
	public class EditorProjectDataModel : EditorProjectDataModelBase
	{
		public readonly static Version VERSION = new(0, 5, 5);
		public override Version Version => VERSION;
	}
}
