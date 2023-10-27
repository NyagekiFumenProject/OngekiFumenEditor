using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects
{
	public class LaneBlockArea : OngekiTimelineObjectBase
	{
		public enum BlockDirection
		{
			Left = 1,
			Right = -1
		}

		public class LaneBlockAreaEndIndicator : OngekiTimelineObjectBase
		{
			public override string IDShortName => "[LBK_End]";

			public LaneBlockArea RefLaneBlockArea { get; internal protected set; }

			public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => IDisplayableObject.EmptyDisplayable;

			private bool tGridHasSet;

			public override TGrid TGrid
			{
				get
				{
					if (!tGridHasSet)
					{
						TGrid = RefLaneBlockArea.TGrid.CopyNew();
						return TGrid;
					}
					return base.TGrid;
				}
				set
				{
					base.TGrid = value is not null ? MathUtils.Max(value, RefLaneBlockArea.TGrid.CopyNew()) : value;
					tGridHasSet = true;
				}
			}

			public override string ToString() => $"{base.ToString()}";
		}

		private IDisplayableObject[] displayables;

		public LaneBlockArea()
		{
			EndIndicator = new LaneBlockAreaEndIndicator() { RefLaneBlockArea = this };
			EndIndicator.PropertyChanged += EndIndicator_PropertyChanged;
			displayables = new IDisplayableObject[] { this, EndIndicator };
		}

		public override TGrid TGrid
		{
			get => base.TGrid;
			set
			{
				base.TGrid = value;
				if (value is not null)
					EndIndicator.TGrid = MathUtils.Max(value.CopyNew(), EndIndicator.TGrid);
			}
		}

		private void EndIndicator_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			NotifyOfPropertyChange(e.PropertyName);
		}

		public override IEnumerable<IDisplayableObject> GetDisplayableObjects() => displayables;

		public override string IDShortName => "LBK";

		public LaneBlockAreaEndIndicator EndIndicator { get; }

		private BlockDirection direction = BlockDirection.Left;
		public BlockDirection Direction
		{
			get => direction;
			set => Set(ref direction, value);
		}

		public (LaneStartBase startWallLane, LaneStartBase endWallLane) CalculateReferenceWallLanes(OngekiFumen fumen)
		{
			var wallType = Direction == BlockDirection.Left ? LaneType.WallLeft : LaneType.WallRight;

			var blockStartTGrid = TGrid;
			var blockEndTGrid = EndIndicator.TGrid;

			var startWallLane = fumen.Lanes.Where(x => x.LaneType == wallType).LastOrDefault(x => x.TGrid <= blockStartTGrid);
			var endWallLane = fumen.Lanes.Where(x => x.LaneType == wallType).LastOrDefault(x => x.TGrid <= blockEndTGrid);

			return (startWallLane, endWallLane);
		}

		public override string ToString() => $"{base.ToString()} Direction[{Direction}] End[{EndIndicator}]";

		public IEnumerable<LaneStartBase> GetAffactableWallLanes(OngekiFumen fumen)
		{
			var blockStartTGrid = TGrid;
			var blockEndTGrid = EndIndicator.TGrid;
			var wallType = Direction == BlockDirection.Left ? LaneType.WallLeft : LaneType.WallRight;
			return fumen.Lanes.GetVisibleStartObjects(blockStartTGrid, blockEndTGrid).Where(x => x.LaneType == wallType);
		}

		public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
		{
			if (maxVisibleTGrid < TGrid)
				return false;

			if (EndIndicator.TGrid < minVisibleTGrid)
				return false;

			return true;
		}

		public void CopyEntire(LaneBlockArea from)
		{
			Copy(from);
			Direction = from.Direction;
			EndIndicator.Copy(from.EndIndicator);
		}
	}
}
