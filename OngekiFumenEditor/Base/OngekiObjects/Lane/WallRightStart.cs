using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
	public class WallRightStart : WallStartBase
	{
		public override string IDShortName => "WRS";

		public override LaneType LaneType => LaneType.WallRight;

		public override ConnectableNextObject CreateNextObject() => new WallRightNext();
		public override ConnectableEndObject CreateEndObject() => new WallRightEnd();
	}
}
