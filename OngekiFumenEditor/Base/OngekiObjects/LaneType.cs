using System;
using System.Windows.Media;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public enum LaneType
	{
		Left = 2,
		Center = 0,
		Right = 3,
		Colorful = 4,
		Enemy = 5,
		Beam = 6,
		WallLeft = 1,
		WallRight = -1,
		//-------------
		AutoPlayFader = 1000,
	}

	public static class LaneTypeExtensions
	{
		public static ConnectableStartObject CreateStartConnectable(this LaneType laneType)
		{
			return laneType switch
			{
				LaneType.Left => new LaneLeftStart(),
				LaneType.Center => new LaneCenterStart(),
				LaneType.Right => new LaneRightStart(),
				LaneType.WallLeft => new WallLeftStart(),
				LaneType.WallRight => new WallRightStart(),
				LaneType.Colorful => new ColorfulLaneStart(),
				LaneType.Beam => new BeamStart(),
				LaneType.Enemy => new EnemyLaneStart(),
				LaneType.AutoPlayFader => new AutoplayFaderLaneStart(),
				_ => throw new ArgumentOutOfRangeException(nameof(laneType), laneType, null)
			};
		}

		public static ConnectableChildObjectBase CreateChildConnectable(this LaneType laneType)
		{
			return laneType switch
			{
				LaneType.Left => new LaneLeftNext(),
				LaneType.Center => new LaneCenterNext(),
				LaneType.Right => new LaneRightNext(),
				LaneType.WallLeft => new WallLeftNext(),
				LaneType.WallRight => new WallRightNext(),
				LaneType.Colorful => new ColorfulLaneNext(),
				LaneType.Beam => new BeamNext(),
				LaneType.Enemy => new EnemyLaneNext(),
				LaneType.AutoPlayFader => new AutoplayFaderLaneNext(),
				_ => throw new ArgumentOutOfRangeException(nameof(laneType), laneType, null)
			};
		}
	}
}
