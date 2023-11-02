using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
	public class WallLeftStart : WallStartBase
	{
		public override string IDShortName => "WLS";

		public override LaneType LaneType => LaneType.WallLeft;

		public override ConnectableChildObjectBase CreateChildObject() => new WallLeftNext();
	}
}
