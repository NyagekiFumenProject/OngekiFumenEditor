using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
	public class WallRightStart : WallStartBase
	{
		public override string IDShortName => "WRS";

		public override LaneType LaneType => LaneType.WallRight;

		public override ConnectableChildObjectBase CreateChildObject() => new WallRightNext();
	}
}
