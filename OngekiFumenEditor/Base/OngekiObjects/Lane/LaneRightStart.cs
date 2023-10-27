using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class LaneRightStart : LaneStartBase
	{
		public override string IDShortName => "LRS";
		public override LaneType LaneType => LaneType.Right;

		public override ConnectableNextObject CreateNextObject() => new LaneRightNext();
		public override ConnectableEndObject CreateEndObject() => new LaneRightEnd();
	}
}
