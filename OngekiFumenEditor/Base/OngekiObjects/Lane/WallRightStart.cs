using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class WallRightStart : WallStartBase
	{
		public override string IDShortName => "WRS";

		public override LaneType LaneType => LaneType.WallRight;

		public override ConnectableChildObjectBase CreateChildObject() => new WallRightNext();
	}
}
