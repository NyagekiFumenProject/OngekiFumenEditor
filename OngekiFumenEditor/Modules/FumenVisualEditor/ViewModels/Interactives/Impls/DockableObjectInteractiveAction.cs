using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
	public class DockableObjectInteractiveAction : DefaultObjectInteractiveAction
	{
		public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
		{
			return editor.Fumen.Lanes.Where(x => x.IsDockableLane).FilterNull();
		}

		public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
		{
			var forceMagneticToLane = editor.Setting.ForceTapHoldMagneticDockToLane;
			var forceMagnetic = editor.Setting.ForceMagneticDock;
			var enableMoveTo = !forceMagneticToLane;

			var dockable = (ILaneDockable)obj;

			if (CheckAndAdjustY(dockable, relativePoint.Y, editor) is double y && TGridCalculator.ConvertYToTGrid_DesignMode(y, editor) is TGrid tGrid)
			{
				var closestLaneObjects = PickDockableObjects(editor)
					.Select(x => x switch
					{
						ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
						LaneStartBase start => start,
						_ => default
					})
					.FilterNull()
					.Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
					.Where(x => x.Item1 != null)
					.Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
					.GroupBy(x => x.Item1)
					.OrderBy(x => x.Key)
					.FirstOrDefault();

				var closestLaneObject = closestLaneObjects?
					.OrderByDescending(x => x.startObject.RecordId)
					.FirstOrDefault();

				/*
                var closestLaneObject = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Where(x => x is not IColorfulLane)
                    .Select(startObject => (CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
                    .Where(x => x.Item1 != null)
                    .Select(x => (Math.Abs(x.Item1.Value - relativePoint.X), x.Item1.Value, x.startObject))
                    .OrderBy(x => x.Item1)
                    .FirstOrDefault();
                */

				var magneticDockDistance = forceMagneticToLane || forceMagnetic ? int.MaxValue : 8;

				if (closestLaneObject?.startObject is not null)
				{
					//如果已经附着到轨道的话，那就考虑如果拖动到另一条线上过近，或者最近的线依旧是自己附属的，
					//那么就强制更新物件的水平位置成对应轨道的
					if (closestLaneObject?.Item1 < magneticDockDistance || //可能拖动到另一条线上
						closestLaneObject?.startObject == dockable.ReferenceLaneStart) //没拖到另一条线上(但还是要更新水平位置)
					{
						relativePoint.X = closestLaneObject?.Value ?? default;
						dockable.ReferenceLaneStart = closestLaneObject?.startObject;
						//Log.LogDebug($"auto dock to lane : {closestLaneObject.startObject}");
						enableMoveTo = true;
					}
				}
			}

			//如果ForceTapHoldMagneticDockToLane=true,则不需要这里钦定位置
			if (enableMoveTo)
				base.OnMoveCanvas(obj, relativePoint, editor);
		}

		public override double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
		{
			if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject)
				return x;
			return base.CheckAndAdjustX(obj, x, editor);
		}

		public override double? CheckAndAdjustY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
		{
			var dockable = (ILaneDockable)obj;
			if (dockable.ReferenceLaneStart is not null)
			{
				if (TGridCalculator.ConvertYToTGrid_DesignMode(y, editor) is not TGrid tGrid)
					return default;
				if (tGrid < dockable.ReferenceLaneStart.MinTGrid)
					return TGridCalculator.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MinTGrid, editor);
				if (tGrid > dockable.ReferenceLaneStart.MaxTGrid)
					return TGridCalculator.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MaxTGrid, editor);
			}

			return base.CheckAndAdjustY(obj, y, editor);
		}

		protected virtual double? CalculateConnectableObjectCurrentRelativeX(ConnectableStartObject startObject, TGrid tGrid, FumenVisualEditorViewModel editor)
		{
			if (tGrid < startObject.TGrid)
				return default;

			var xGrid = startObject.CalulateXGrid(tGrid);
			if (xGrid == null)
				return default;

			return XGridCalculator.ConvertXGridToX(xGrid, editor);
		}
	}
}
