using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane
{
	public class LaneRightStart : LaneStartBase
	{
		public override string IDShortName => "LRS";
		public override LaneType LaneType => LaneType.Right;

		public override ConnectableChildObjectBase CreateChildObject() => new LaneRightNext();
	}
}

