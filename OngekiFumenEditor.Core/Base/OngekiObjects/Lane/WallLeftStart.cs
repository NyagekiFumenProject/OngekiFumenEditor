using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class WallLeftStart : WallStartBase
	{
		public override string IDShortName => "WLS";

		public override LaneType LaneType => LaneType.WallLeft;

		public override ConnectableChildObjectBase CreateChildObject() => new WallLeftNext();
	}
}
