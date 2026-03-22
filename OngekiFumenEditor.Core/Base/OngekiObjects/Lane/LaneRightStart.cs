using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class LaneRightStart : LaneStartBase
	{
		public override string IDShortName => "LRS";
		public override LaneType LaneType => LaneType.Right;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneRightNext();
	}
}
