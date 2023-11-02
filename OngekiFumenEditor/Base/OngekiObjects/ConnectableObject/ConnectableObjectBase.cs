using OngekiFumenEditor.Utils;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
	public abstract class ConnectableObjectBase : OngekiMovableObjectBase
	{
		public abstract int RecordId { get; set; }

		public abstract LaneType LaneType { get; }

		public bool IsWallLane => LaneType == LaneType.WallLeft || LaneType.WallRight == LaneType;

		public bool IsDockableLane => LaneType switch
		{
			LaneType.Right or LaneType.Center or LaneType.Left => true,
			LaneType.WallLeft or LaneType.WallRight => true,
			_ => false
		};

		public override string ToString() => $"{base.ToString()} RID[{RecordId}]";

		public override void Copy(OngekiObjectBase fromObj)
		{
			base.Copy(fromObj);

			if (fromObj is not ConnectableObjectBase from)
				return;

			RecordId = from.RecordId;
		}

		public ConnectableChildObjectBase NextObject { get; set; }

		public abstract ConnectableStartObject ReferenceStartObject { get;}

		public static void RelocateDockableObjects(OngekiFumen fumen, ConnectableObjectBase affactedObj)
		{
			var start = affactedObj.ReferenceStartObject;

			RelocateDockableObjects(fumen, affactedObj, start);
			if (affactedObj is ConnectableChildObjectBase child)
				RelocateDockableObjects(fumen, child.PrevObject, start);
		}

		public static void RelocateDockableAllObjects(OngekiFumen fumen, ConnectableStartObject start)
		{
			foreach (var child in start.Children)
				RelocateDockableObjects(fumen, child.PrevObject, start);
		}

		public static void RelocateDockableObjects(OngekiFumen fumen, ConnectableObjectBase obj, ConnectableStartObject start)
		{
			if (start is null ||
				obj is null ||
				obj.NextObject is null)
				return;
			var refLaneId = obj.RecordId;

			var minTGrid = obj.TGrid;
			var maxTGrid = obj.NextObject.TGrid;

			using var _ = fumen.GetAllDisplayableObjects(minTGrid, maxTGrid)
				.OfType<ILaneDockable>()
				.Where(x => x.ReferenceLaneStrId == refLaneId)
				.Where(x => !((ISelectableObject)x).IsSelected)
				.ToHashSetWithObjectPool(out var dockables);

			foreach (var dockable in dockables)
			{
				if (start.CalulateXGrid(dockable.TGrid) is XGrid xGrid)
					dockable.XGrid = xGrid;

				if (dockable is Hold hold && hold.HoldEnd is HoldEnd end)
				{
					if (end.RefHold?.ReferenceLaneStart?.CalulateXGrid(end.TGrid) is XGrid xGrid2)
						end.XGrid = xGrid2;
				}
			}
		}
	}
}
