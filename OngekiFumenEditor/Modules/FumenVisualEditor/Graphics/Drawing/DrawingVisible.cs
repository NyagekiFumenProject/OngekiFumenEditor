using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
	[Flags]
	public enum DrawingVisible
	{
		None = 0,

		Design = 1,
		Preview = 2,

		All = Design | Preview,
	}
}
