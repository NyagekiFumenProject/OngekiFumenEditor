using System;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
	// Legacy 0.5.4 files used the same locator-bearing contract as 0.5.2.
	public class EditorProjectDataModel_V0_5_4 : EditorProjectDataModel_V0_5_2
	{
		public new readonly static Version VERSION = new(0, 5, 4);

		public override Version Version => VERSION;
	}
}
