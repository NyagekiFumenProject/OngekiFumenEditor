using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane
{
	public class EnemyLaneStart : LaneStartBase
	{
		public override string IDShortName => "ENS";

		public override LaneType LaneType => LaneType.Enemy;

		public override ConnectableChildObjectBase CreateChildObject() => new EnemyLaneNext();
	}
}

