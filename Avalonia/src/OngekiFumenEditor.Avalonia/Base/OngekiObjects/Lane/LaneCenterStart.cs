using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane
{
	public class LaneCenterStart : LaneStartBase
	{
		public override string IDShortName => "LCS";

		public override LaneType LaneType => LaneType.Center;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneCenterNext();
	}
}

