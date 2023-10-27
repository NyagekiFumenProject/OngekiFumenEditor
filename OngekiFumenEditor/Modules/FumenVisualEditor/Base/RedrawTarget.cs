using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
	[Flags]
	public enum RedrawTarget
	{
		OngekiObjects = 1,
		TGridUnitLines = 2,
		XGridUnitLines = 4,
		ScrollBar = 8,

		All = OngekiObjects | TGridUnitLines | XGridUnitLines | ScrollBar,
		UnitLines = TGridUnitLines | XGridUnitLines,
	}
}
