using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
	public class EnemyLaneStart : LaneStartBase
	{
		public override string IDShortName => "ENS";

		public override LaneType LaneType => LaneType.Enemy;

		public override ConnectableNextObject CreateNextObject() => new EnemyLaneNext();
		public override ConnectableEndObject CreateEndObject() => new EnemyLaneEnd();
	}
}
