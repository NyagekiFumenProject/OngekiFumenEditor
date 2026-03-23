using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.Lane
{
	public class LaneRightStart : LaneStartBase
	{
		public override string IDShortName => "LRS";
		public override LaneType LaneType => LaneType.Right;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneRightNext();
	}
}
