using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class LaneCenterStart : LaneStartBase
	{
		public override string IDShortName => "LCS";

		public override LaneType LaneType => LaneType.Center;

		public override ConnectableNextObject CreateNextObject() => new LaneCenterNext();
		public override ConnectableEndObject CreateEndObject() => new LaneCenterEnd();
	}
}
