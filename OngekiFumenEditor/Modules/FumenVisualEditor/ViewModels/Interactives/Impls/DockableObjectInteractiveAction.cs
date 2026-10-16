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
        private bool isXGridSnappedToLaneThisMove;

        protected override bool IsXGridSnappedToLaneThisMove => isXGridSnappedToLaneThisMove;

        public virtual IEnumerable<ConnectableObjectBase> PickDockableObjects(FumenVisualEditorViewModel editor = default)
        {
            return editor.Fumen.Lanes.Where(x => x.IsDockableLane).FilterNull();
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point relativePoint, FumenVisualEditorViewModel editor)
        {
            isXGridSnappedToLaneThisMove = false;

            // 已附轨物件保持原轨引用，时间吸附遵循通用开关；轨道区间外不夹时间或跨轨。
            if (obj is ILaneDockable attached && attached.ReferenceLaneStart is LaneStartBase originalLane)
            {
                var basic = new DefaultObjectInteractiveAction();
                if (basic.CheckAndAdjustY(attached, relativePoint.Y, editor) is double attachedY &&
                    editor.ConvertYToTGrid_DesignMode(attachedY) is TGrid attachedT)
                {
                    var targetX = XGridCalculator.ConvertXToXGrid(relativePoint.X, editor);
                    var attachedX = CalculateOriginalLaneX(originalLane, attachedT, targetX);
                    if (attachedX == null) return;
                    if (attached.TGrid.TotalGrid != attachedT.TotalGrid) attached.TGrid = attachedT;
                    if (attached.XGrid.TotalGrid != attachedX.TotalGrid) attached.XGrid = attachedX;
                }
                return;
            }

            var forceMagneticToLane = editor.Setting.ForceTapHoldMagneticDockToLane;
            var forceMagnetic = editor.Setting.ForceMagneticDock;
            var enableMoveTo = !forceMagneticToLane;

            var dockable = (ILaneDockable)obj;

            if (CheckAndAdjustY(dockable, relativePoint.Y, editor) is double y && editor.ConvertYToTGrid_DesignMode(y) is TGrid tGrid)
            {
                var closestLaneObjects = PickDockableObjects(editor)
                    .Select(x => x switch
                    {
                        ConnectableChildObjectBase child => child.ReferenceStartObject as LaneStartBase,
                        LaneStartBase start => start,
                        _ => default
                    })
                    .FilterNull()
                    .Select(startObject => (position: CalculateConnectableObjectCurrentRelativeX(startObject, tGrid, editor), startObject))
                    .Where(x => x.position != null)
                    .Select(candidate =>
                    {
                        var dockX = candidate.position.Value;
                        if (TryGetHorizontalSegmentDockingX(candidate.startObject, tGrid, relativePoint.X, editor, out var horizontalX))
                            dockX = horizontalX;

                        return (Math.Abs(dockX - relativePoint.X), Value: dockX, candidate.startObject);
                    })
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
                        isXGridSnappedToLaneThisMove = true;
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

        // 同时刻水平段覆盖一段 X 区间，保留目标横坐标在最近区间内的位置。
        internal static XGrid CalculateOriginalLaneX(LaneStartBase lane, TGrid time, XGrid target)
            => CreateOriginalLaneProjector(lane)(time, target);

        // 每个动作按轨道建一次索引；有效性仅检查一次，同 T 插值结果复用。
        internal static Func<TGrid, XGrid, XGrid> CreateOriginalLaneProjector(LaneStartBase lane)
        {
            var children = lane.Children.ToArray();
            var valid = lane.IsPathVaild();
            var horizontalRanges = new Dictionary<int, List<(int min, int max)>>();
            foreach (var child in children)
            {
                if (child.PrevObject is not OngekiMovableObjectBase previous || previous.TGrid.TotalGrid != child.TGrid.TotalGrid) continue;
                var t = child.TGrid.TotalGrid;
                var min = Math.Min(previous.XGrid.TotalGrid, child.XGrid.TotalGrid);
                var max = Math.Max(previous.XGrid.TotalGrid, child.XGrid.TotalGrid);
                var horizontal = true;
                foreach (var control in child.PathControls)
                {
                    if (control.TGrid.TotalGrid != t) { horizontal = false; break; }
                    min = Math.Min(min, control.XGrid.TotalGrid);
                    max = Math.Max(max, control.XGrid.TotalGrid);
                }
                if (!horizontal) continue;
                if (child.PathControls.Count > 0)
                {
                    var path = child.GetConnectionPaths().Select(p => p.pos.X).ToArray();
                    if (path.Length > 0) { min = (int)Math.Ceiling(path.Min()); max = (int)Math.Floor(path.Max()); }
                }
                if (!horizontalRanges.TryGetValue(t, out var ranges)) horizontalRanges[t] = ranges = new();
                ranges.Add((min, max));
            }
            var interpolated = new Dictionary<int, XGrid>();
            return (time, target) =>
            {
                if (horizontalRanges.TryGetValue(time.TotalGrid, out var ranges))
                {
                    int closest = target.TotalGrid;
                    long distance = long.MaxValue;
                    foreach (var range in ranges)
                    {
                        var x = Math.Max(range.min, Math.Min(range.max, target.TotalGrid));
                        var delta = Math.Abs((long)x - target.TotalGrid);
                        if (delta >= distance) continue;
                        distance = delta; closest = x;
                    }
                    return XGrid.FromTotalGrid(closest);
                }
                if (interpolated.TryGetValue(time.TotalGrid, out var cached)) return cached;
                XGrid result = null;
                if (!valid) result = lane.CalulateXGrid(time);
                else if (time >= lane.TGrid && children.Length > 0)
                {
                    var lo = 0; var hi = children.Length;
                    while (lo < hi)
                    {
                        var mid = lo + (hi - lo) / 2;
                        if (children[mid].TGrid < time) lo = mid + 1;
                        else hi = mid;
                    }
                    if (lo < children.Length) result = children[lo].CalulateXGrid(time);
                }
                interpolated[time.TotalGrid] = result;
                return result;
            };
        }

        public override double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject startObject &&
                obj is ITimelineObject timelineObject &&
                TryGetHorizontalSegmentXRange(startObject, timelineObject.TGrid, x, editor, out var minX, out var maxX))
            {
                // A zero-duration lane segment covers an X range. Apply the normal XGrid
                // adjustment first, then keep the result inside that range.
                var adjustedX = base.CheckAndAdjustX(obj, x, editor);
                if (adjustedX is not double value)
                    return adjustedX;

                return Math.Max(minX, Math.Min(maxX, value));
            }

            if (((ILaneDockable)obj).ReferenceLaneStart is ConnectableStartObject)
                return x;

            return base.CheckAndAdjustX(obj, x, editor);
        }

        public override double? CheckAndAdjustY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
        {
            var dockable = (ILaneDockable)obj;
            if (dockable.ReferenceLaneStart is not null)
            {
                if (editor.ConvertYToTGrid_DesignMode(y) is not TGrid tGrid)
                    return default;
                if (tGrid < dockable.ReferenceLaneStart.MinTGrid)
                    return editor.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MinTGrid);
                if (tGrid > dockable.ReferenceLaneStart.MaxTGrid)
                    return editor.ConvertTGridToY_DesignMode(dockable.ReferenceLaneStart.MaxTGrid);
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

        /// <summary>
        /// Returns the position on a horizontal lane segment that is closest to the dragged object.
        /// A zero-duration segment spans an X interval at one TGrid, while the regular interpolation
        /// API can only return its first key. Keeping the dragged X inside that interval lets a
        /// docked Tap or Hold move between the segment's endpoints.
        /// </summary>
        protected static bool TryGetHorizontalSegmentDockingX(
            ConnectableStartObject startObject,
            TGrid tGrid,
            double draggedX,
            FumenVisualEditorViewModel editor,
            out double dockingX)
        {
            dockingX = default;
            if (!TryGetHorizontalSegmentXRange(startObject, tGrid, draggedX, editor, out var minX, out var maxX))
                return false;

            dockingX = Math.Max(minX, Math.Min(maxX, draggedX));
            return true;
        }

        /// <summary>
        /// Finds the horizontal zero-duration segment nearest to the dragged position and returns its X range.
        /// </summary>
        protected static bool TryGetHorizontalSegmentXRange(
            ConnectableStartObject startObject,
            TGrid tGrid,
            double draggedX,
            FumenVisualEditorViewModel editor,
            out double minX,
            out double maxX)
        {
            minX = default;
            maxX = default;
            var totalTGrid = tGrid.TotalGrid;
            var hasCandidate = false;
            var closestDistance = double.MaxValue;

            foreach (var child in startObject.Children)
            {
                if (child.PrevObject is not OngekiMovableObjectBase previousObject ||
                    previousObject.TGrid.TotalGrid != totalTGrid ||
                    child.TGrid.TotalGrid != totalTGrid)
                    continue;

                var minXTotalGrid = (double)previousObject.XGrid.TotalGrid;
                var maxXTotalGrid = (double)child.XGrid.TotalGrid;
                var isHorizontal = true;
                foreach (var control in child.PathControls)
                {
                    if (control.TGrid.TotalGrid != totalTGrid)
                    {
                        isHorizontal = false;
                        break;
                    }

                    minXTotalGrid = Math.Min(minXTotalGrid, control.XGrid.TotalGrid);
                    maxXTotalGrid = Math.Max(maxXTotalGrid, control.XGrid.TotalGrid);
                }

                if (!isHorizontal)
                    continue;

                var candidateMinX = XGridCalculator.ConvertXGridToX(minXTotalGrid / XGrid.DEFAULT_RES_X, editor);
                var candidateMaxX = XGridCalculator.ConvertXGridToX(maxXTotalGrid / XGrid.DEFAULT_RES_X, editor);
                if (candidateMinX > candidateMaxX)
                    (candidateMinX, candidateMaxX) = (candidateMaxX, candidateMinX);

                var candidateX = Math.Max(candidateMinX, Math.Min(candidateMaxX, draggedX));
                var distance = Math.Abs(candidateX - draggedX);
                if (hasCandidate && distance >= closestDistance)
                    continue;

                hasCandidate = true;
                closestDistance = distance;
                minX = candidateMinX;
                maxX = candidateMaxX;
            }

            return hasCandidate;
        }
    }
}
