using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane
{
	public class WallLeftStart : WallStartBase
	{
		public override string IDShortName => "WLS";

		public override LaneType LaneType => LaneType.WallLeft;

		public override ConnectableChildObjectBase CreateChildObject() => new WallLeftNext();
	}
}
