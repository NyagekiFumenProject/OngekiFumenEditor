using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane
{
	public class LaneLeftStart : LaneStartBase
	{
		public override string IDShortName => "LLS";

		public override LaneType LaneType => LaneType.Left;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneLeftNext();
	}
}
