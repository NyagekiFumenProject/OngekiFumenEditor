using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.Lane
{
	public class WallRightStart : WallStartBase
	{
		public override string IDShortName => "WRS";

		public override LaneType LaneType => LaneType.WallRight;

		public override ConnectableChildObjectBase CreateChildObject() => new WallRightNext();
	}
}
