using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
	internal class ConnectableObjectInteractiveAction : DefaultObjectInteractiveAction
	{
		private struct DragInfo
		{
			public ILaneDockable Dockable { get; set; }

			public XGrid XGrid { get; set; }
			public TGrid TGrid { get; set; }

			public LaneStartBase RefLane { get; set; }
		}

		private Dictionary<OngekiObjectBase, HashSet<DragInfo>> dragInfoMap = new();

		public override void OnMoveCanvas(OngekiObjectBase o, Point point, FumenVisualEditorViewModel editor)
		{
			base.OnMoveCanvas(o, point, editor);
			var obj = o switch
			{
				ConnectableObjectBase co => co,
				LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
				_ => default
			};
			ConnectableStartObject.RelocateDockableObjects(editor.Fumen, obj);
		}

		public override void OnDragStart(OngekiObjectBase o, Point pos, FumenVisualEditorViewModel editor)
		{
			base.OnDragStart(o, pos, editor);

			var obj = o switch
			{
				ConnectableObjectBase co => co,
				LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
				_ => default
			};

			var start = obj switch
			{
				ConnectableChildObjectBase c => c.ReferenceStartObject,
				ConnectableStartObject s => s,
				_ => default
			};

			var refLaneId = obj.RecordId;

			var minTGrid = obj.TGrid;
			var maxTGrid = obj.NextObject?.TGrid ?? minTGrid;
			if (obj is ConnectableChildObjectBase child)
				minTGrid = child.PrevObject.TGrid;

			var infoList = editor.Fumen.GetAllDisplayableObjects(minTGrid, maxTGrid)
				.OfType<ILaneDockable>()
				.Where(x => x.ReferenceLaneStrId == refLaneId)
				.Where(x => !((ISelectableObject)x).IsSelected)
				.Select(x =>
				{
					var info = new DragInfo();
					info.Dockable = x;

					if (x is IHorizonPositionObject horizonPositionObject)
						info.XGrid = horizonPositionObject.XGrid.CopyNew();

					if (x is ITimelineObject timelineObject)
						info.TGrid = timelineObject.TGrid.CopyNew();

					info.RefLane = info.Dockable.ReferenceLaneStart;

					return info;
				})
				.ToHashSet();

			dragInfoMap[o] = infoList;
		}

		public override void OnDragEnd(OngekiObjectBase o, Point point, FumenVisualEditorViewModel editor)
		{
			base.OnDragEnd(o, point, editor);

			var obj = o switch
			{
				ConnectableObjectBase co => co,
				LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
				_ => default
			};

			if (dragInfoMap.TryGetValue(o, out var infoList))
				dragInfoMap.Remove(o);
			else
				return;//YOU SHOULD NOT BE HERE

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.UpdateXGridForDockedObjects,
				() =>
				{
					ConnectableStartObject.RelocateDockableObjects(editor.Fumen, obj);
				}, () =>
				{
					foreach (var info in infoList)
					{
						info.Dockable.XGrid = info.XGrid;
						info.Dockable.TGrid = info.TGrid;
						info.Dockable.ReferenceLaneStart = info.RefLane;
					}
				}));
		}

	}
}
