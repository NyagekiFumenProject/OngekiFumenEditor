using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.Lane
{
	public class WallLeftStart : WallStartBase
	{
		public override string IDShortName => "WLS";

		public override LaneType LaneType => LaneType.WallLeft;

		public override ConnectableChildObjectBase CreateChildObject() => new WallLeftNext();
	}
}
