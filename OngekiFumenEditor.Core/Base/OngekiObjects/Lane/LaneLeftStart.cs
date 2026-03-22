using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class LaneLeftStart : LaneStartBase
	{
		public override string IDShortName => "LLS";

		public override LaneType LaneType => LaneType.Left;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneLeftNext();
	}
}
