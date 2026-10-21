using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
    // 一次选择拖动的关联模型快照，生命周期与提交由 ObjectInteractiveManager 管理。
    // Hold/轨道关联必须先整组预检，不能逐物件调用有副作用的 OnMoveCanvas。
    internal sealed class SelectionGridDrag
    {
        internal sealed class Position
        {
            internal OngekiTimelineObjectBase Object;
            internal int T;
            internal int? X;
            internal LaneStartBase Lane;
            internal bool Selected;
            internal bool LaneAffected;
        }

        private readonly Dictionary<OngekiTimelineObjectBase, Position> positionsByObject;
        internal readonly Position[] Before;
        internal readonly long AnchorT;
        internal readonly long AnchorX;
        internal Point GrabOffset;
        private readonly bool allowLaneSwitch;
        private LaneSwitchResolver laneSwitchResolver;

        internal SelectionGridDrag(IEnumerable<OngekiTimelineObjectBase> selected, IEnumerable<OngekiTimelineObjectBase> related)
        {
            var targets = selected.ToHashSet();
            var changedLanes = targets.Select(o => o switch
            {
                ConnectableObjectBase c => c.ReferenceStartObject,
                LaneCurvePathControlObject control => control.RefCurveObject?.ReferenceStartObject,
                _ => null
            }).Where(x => x != null).ToHashSet();
            allowLaneSwitch = changedLanes.Count == 0;
            Before = related.Concat(targets).Distinct().Select(o => new Position
            {
                Object = o,
                T = o.TGrid.TotalGrid,
                X = (o as IHorizonPositionObject)?.XGrid.TotalGrid,
                Lane = (o as ILaneDockable)?.ReferenceLaneStart,
                Selected = targets.Contains(o),
                LaneAffected = o is ILaneDockable dock && changedLanes.Contains(dock.ReferenceLaneStart)
            }).ToArray();
            positionsByObject = Before.ToDictionary(p => p.Object);
            AnchorT = Before.Where(p => p.Selected).Min(p => (long)p.T);
            var xs = Before.Where(p => p.Selected && p.X.HasValue).Select(p => (long)p.X.Value).ToArray();
            AnchorX = xs.Length == 0 ? 0 : (long)Math.Round((xs.Min() + xs.Max()) / 2d);
        }

        internal bool TryMove(long dt, long dx) => TryMoveWithSnapping(dt, dx, false);

        internal bool TryMoveWithSnapping(long dt, long dx, bool snapHoldEndsToLane) => TryMoveCore(dt, dx, snapHoldEndsToLane, null);

        internal bool TryMoveInEditor(long dt, long dx, FumenVisualEditorViewModel editor) => TryMoveCore(dt, dx, true, editor);

        private bool TryMoveCore(long dt, long dx, bool snapToLane, FumenVisualEditorViewModel editor)
        {
            var desired = new Position[Before.Length];
            for (var i = 0; i < Before.Length; i++)
            {
                var p = Before[i];
                var t = (long)p.T + (p.Selected ? dt : 0);
                var x = p.X.HasValue ? (long)p.X.Value + (p.Selected ? dx : 0) : 0;
                if (t < 0 || t > int.MaxValue || x < int.MinValue || x > int.MaxValue)
                    return false;
                var targetT = TGrid.FromTotalGrid((int)t);
                XGrid targetX = p.X.HasValue ? XGrid.FromTotalGrid((int)x) : null;
                if (p.Selected && editor != null && !editor.InteractiveManager.TryAdjustGrid(p.Object, ref targetT, ref targetX, editor,
                    dt == 0 && p.Object is Tap or Hold or HoldEnd))
                    return false;

                desired[i] = new Position
                {
                    Object = p.Object,
                    T = targetT.TotalGrid,
                    X = targetX?.TotalGrid,
                    Lane = p.Lane,
                    Selected = p.Selected,
                    LaneAffected = p.LaneAffected
                };
            }

            var byObject = desired.ToDictionary(p => p.Object);
            // 同选头尾以头部的实际时间位移为准，避免分别吸附改变 Hold 时长。
            foreach (var head in desired.Where(p => p.Selected && p.Object is Hold))
            {
                var hold = (Hold)head.Object;
                if (hold.HoldEnd != null && byObject.TryGetValue(hold.HoldEnd, out var tail) && tail.Selected)
                {
                    var end = (long)head.T + positionsByObject[hold.HoldEnd].T - positionsByObject[hold].T;
                    if (end < head.T || end > int.MaxValue)
                        return false;
                    tail.T = (int)end;
                }
            }

            if (editor != null && allowLaneSwitch)
            {
                foreach (var p in desired.Where(p => p.Selected && p.Object is Tap or Hold))
                {
                    var currentLane = ((ILaneDockable)p.Object).ReferenceLaneStart;
                    var hold = p.Object as Hold;
                    Position tail = null;
                    if (hold?.HoldEnd != null)
                        byObject.TryGetValue(hold.HoldEnd, out tail);
                    var end = hold?.HoldEnd == null ? null : TGrid.FromTotalGrid(tail?.T ?? hold.HoldEnd.TGrid.TotalGrid);
                    if (end != null && end.TotalGrid < p.T)
                        return false;
                    laneSwitchResolver ??= editor.InteractiveManager.CreateLaneSwitchResolver(editor);
                    var rawX = XGrid.FromTotalGrid((int)((long)positionsByObject[p.Object].X.Value + dx));
                    p.Lane = currentLane == null || dx != 0 || !ReferenceEquals(currentLane, p.Lane)
                        ? laneSwitchResolver.Resolve(currentLane, TGrid.FromTotalGrid(p.T), rawX, end) : currentLane;
                    if (p.Lane == null)
                    {
                        continue;
                    }
                    // 在写入模型前先投影，避免每帧先写鼠标 X 再贴轨造成重复通知。
                    var headX = laneSwitchResolver.Project(p.Lane, TGrid.FromTotalGrid(p.T), rawX);
                    if (headX == null)
                        return false;
                    p.X = headX.TotalGrid;
                    if (tail != null)
                    {
                        tail.Lane = p.Lane;
                        tail.LaneAffected = true;
                        var tailX = laneSwitchResolver.Project(p.Lane, TGrid.FromTotalGrid(tail.T), XGrid.FromTotalGrid(tail.X.Value));
                        if (tailX == null)
                            return false;
                        tail.X = tailX.TotalGrid;
                    }
                }
            }

            // 全部关键节点选中的整线使用实际起点的吸附增量，保留几何形状。
            foreach (var lane in desired.Where(p => p.Object is ConnectableStartObject).Select(p => (ConnectableStartObject)p.Object))
            {
                var points = lane.Children.Cast<OngekiTimelineObjectBase>().Prepend(lane).ToArray();
                if (!points.All(o => byObject.TryGetValue(o, out var p) && p.Selected))
                    continue;
                var start = byObject[lane];
                var original = positionsByObject[lane];
                var laneDt = (long)start.T - original.T;
                var laneDx = (long)(start.X ?? 0) - (original.X ?? 0);
                foreach (var o in points.Concat(lane.Children.SelectMany(c => c.PathControls).Cast<OngekiTimelineObjectBase>()))
                    if (byObject.TryGetValue(o, out var p) && positionsByObject.TryGetValue(o, out var before))
                    {
                        var t = before.T + laneDt;
                        var x = before.X.HasValue ? before.X.Value + laneDx : 0;
                        if (t < 0 || t > int.MaxValue || x < int.MinValue || x > int.MaxValue)
                            return false;
                        p.T = (int)t;
                        p.X = before.X.HasValue ? (int?)x : null;
                    }
            }

            if (snapToLane)
            {
                var affectedLanes = desired.Where(p => p.LaneAffected && p.Lane != null).Select(p => p.Lane).Distinct().ToArray();
                bool HadOriginalCoverage(Position note, LaneStartBase lane)
                {
                    if (note.Selected)
                        return true;
                    var firstT = positionsByObject.TryGetValue(lane, out var first) ? first.T : lane.TGrid.TotalGrid;
                    var lastNode = lane.Children.LastOrDefault();
                    var lastT = lastNode != null && positionsByObject.TryGetValue(lastNode, out var last) ? last.T : lastNode?.TGrid.TotalGrid ?? firstT;
                    var originalT = positionsByObject[note.Object].T;
                    return originalT >= firstT && originalT <= lastT;
                }

                var wholeLanes = new HashSet<LaneStartBase>();
                long minCorrection = long.MinValue, maxCorrection = long.MaxValue;
                foreach (var lane in affectedLanes)
                {
                    var points = lane.Children.Cast<OngekiTimelineObjectBase>().Prepend(lane).ToArray();
                    if (!points.All(o => byObject.TryGetValue(o, out var p) && p.Selected))
                        continue;
                    wholeLanes.Add(lane);
                    var fixedNotes = desired.Where(p => ReferenceEquals(p.Lane, lane) && !p.Selected && HadOriginalCoverage(p, lane) && p.Object is Tap or Hold or HoldEnd).ToArray();
                    if (fixedNotes.Length == 0 || points.Length < 2)
                        continue;
                    maxCorrection = Math.Min(maxCorrection, (long)fixedNotes.Min(p => p.T) - byObject[lane].T);
                    minCorrection = Math.Max(minCorrection, (long)fixedNotes.Max(p => p.T) - byObject[points.Last()].T);
                }

                if (minCorrection > maxCorrection)
                    return false;
                // 整线遇边界统一限制时间偏移，不把整条线压缩成较短范围。
                var correction = Math.Max(minCorrection, Math.Min(maxCorrection, 0L));
                if (correction != 0)
                    foreach (var p in desired.Where(p => p.Selected || p.Object is LaneCurvePathControlObject control && wholeLanes.Contains(control.RefCurveObject?.ReferenceStartObject as LaneStartBase)))
                    {
                        var t = p.T + correction;
                        if (t < 0 || t > int.MaxValue)
                            return false;
                        p.T = (int)t;
                    }

                foreach (var lane in affectedLanes)
                {
                    if (wholeLanes.Contains(lane))
                        continue;
                    var notes = desired.Where(p => ReferenceEquals(p.Lane, lane) && HadOriginalCoverage(p, lane) && p.Object is Tap or Hold or HoldEnd).ToArray();
                    if (notes.Length == 0)
                        continue;
                    if (byObject.TryGetValue(lane, out var start) && start.Selected)
                        start.T = Math.Min(start.T, notes.Min(p => p.T));
                    if (lane.Children.LastOrDefault() is OngekiTimelineObjectBase last && byObject.TryGetValue(last, out var endPoint) && endPoint.Selected)
                        endPoint.T = Math.Max(endPoint.T, notes.Max(p => p.T));
                }
            }

            long Time(OngekiTimelineObjectBase o) => byObject.TryGetValue(o, out var p) ? p.T : o.TGrid.TotalGrid;
            foreach (var p in desired)
            {
                var end = p.Object switch
                {
                    Hold h => (OngekiTimelineObjectBase)h.HoldEnd,
                    Soflan speed => speed.EndIndicator,
                    LaneBlockArea block => block.EndIndicator,
                    IndividualSoflanArea area => area.EndIndicator,
                    _ => null
                };
                if (p.Object is ConnectableStartObject lane)
                {
                    long previous = p.T;
                    foreach (var child in lane.Children)
                    {
                        var next = Time(child);
                        if (next < previous)
                            return false;
                        previous = next;
                    }
                }

                if (end != null && Time(end) < p.T)
                    return false;
            }

            var previousPositions = snapToLane ? CaptureCurrent() : null;
            if (!ApplyWithSnapping(desired, 0, 0, snapToLane, allowLaneSwitch ? laneSwitchResolver : null))
            {
                Apply(previousPositions);
                return false;
            }

            return true;
        }

        internal static void Apply(Position[] positions, long dt = 0, long dx = 0) => ApplyWithSnapping(positions, dt, dx, false);

        private static bool ApplyWithSnapping(Position[] positions, long dt, long dx, bool snapHoldEndsToLane, LaneSwitchResolver resolver = null)
        {
            // 父物件先写入，尾节点随后恢复，避免持续物件 setter 钳制尾节点。
            foreach (var p in positions.OrderBy(p => p.Object is HoldEnd or Soflan.SoflanEndIndicator or LaneBlockArea.LaneBlockAreaEndIndicator or IndividualSoflanArea.IndividualSoflanAreaEndIndicator ? 1 : 0))
            {
                if (p.Object is ILaneDockable dock && !ReferenceEquals(dock.ReferenceLaneStart, p.Lane))
                    dock.ReferenceLaneStart = p.Lane;
                var targetT = (int)(p.T + (p.Selected ? dt : 0));
                if (p.Object.TGrid.TotalGrid != targetT)
                    p.Object.TGrid = TGrid.FromTotalGrid(targetT);
            }

            // 附轨属性通知可能重算尾部 X；最后统一恢复真实横坐标。
            foreach (var p in positions)
                if (p.Object is IHorizonPositionObject horizontal && p.X is int x)
                {
                    var targetX = (int)(x + (p.Selected ? dx : 0));
                    if (horizontal.XGrid.TotalGrid != targetX)
                        horizontal.XGrid = XGrid.FromTotalGrid(targetX);
                }

            // 全部轨道节点写完后再投影；未选附轨物件仅更新 X，时间保持原快照。
            if (snapHoldEndsToLane)
            {
                var projectors = new Dictionary<LaneStartBase, Func<TGrid, XGrid, XGrid>>();
                XGrid Project(LaneStartBase lane, TGrid time, XGrid x)
                {
                    if (resolver != null)
                        return resolver.Project(lane, time, x);
                    if (!projectors.TryGetValue(lane, out var project))
                        projectors[lane] = project = DockableObjectInteractiveAction.CreateOriginalLaneProjector(lane);
                    return project(time, x);
                }

                foreach (var p in positions)
                    if ((p.Selected || p.LaneAffected) && p.Object is Tap or Hold or HoldEnd && p.Lane != null &&
                        p.Object is IHorizonPositionObject horizontal)
                    {
                        var laneX = Project(p.Lane, p.Object.TGrid, horizontal.XGrid);
                        if (laneX == null)
                            return false;
                        if (horizontal.XGrid.TotalGrid != laneX.TotalGrid)
                            horizontal.XGrid = laneX;
                    }
            }

            return true;
        }

        internal Position[] CaptureCurrent() => Before.Select(p => new Position
        {
            Object = p.Object,
            T = p.Object.TGrid.TotalGrid,
            X = (p.Object as IHorizonPositionObject)?.XGrid.TotalGrid,
            Lane = (p.Object as ILaneDockable)?.ReferenceLaneStart
        }).ToArray();
    }

}
