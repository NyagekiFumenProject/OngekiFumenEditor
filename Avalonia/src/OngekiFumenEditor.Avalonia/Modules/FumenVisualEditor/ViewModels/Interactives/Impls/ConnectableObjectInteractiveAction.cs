using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
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

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.UpdateXGridForDockedObjects.ToLocalizedString(),
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



