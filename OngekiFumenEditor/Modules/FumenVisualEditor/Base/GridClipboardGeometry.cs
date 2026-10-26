using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;


namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    internal static class GridClipboardGeometry
    {
        public static bool PalettesEquivalent(BulletPallete left, BulletPallete right) =>
            left != null && right != null && left.TypeValue == right.TypeValue &&
            left.TargetValue == right.TargetValue && left.ShooterValue == right.ShooterValue &&
            left.PlaceOffset == right.PlaceOffset && left.RandomOffsetRange == right.RandomOffsetRange &&
            left.SizeValue == right.SizeValue && left.Speed.Equals(right.Speed) &&
            left.EditorName == right.EditorName;

        public static OngekiObjectBase[] CloneSet(IEnumerable<OngekiObjectBase> sources)
        {
            var originals = sources.Where(x => x != null).ToArray();
            var copies = originals.Select(Clone).ToArray();
            var lanes = originals.Select((source, index) => (source, copy: copies[index]))
                .Where(x => x.source is OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase && x.copy != null)
                .ToDictionary(x => (OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase)x.source,
                    x => (OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase)x.copy);
            for (var i = 0; i < copies.Length; i++)
            {
                if (originals[i] is ILaneDockable from && copies[i] is ILaneDockable to &&
                    from.ReferenceLaneStart != null && lanes.TryGetValue(from.ReferenceLaneStart, out var lane))
                {
                    var coordinates = Enumerate(copies[i]).Select(x => (node: x,
                        t: (x as ITimelineObject)?.TGrid?.CopyNew(), x: (x as IHorizonPositionObject)?.XGrid?.CopyNew())).ToArray();
                    to.ReferenceLaneStart = lane;
                    foreach (var point in coordinates)
                    {
                        if (point.t != null)
                            ((ITimelineObject)point.node).TGrid = point.t;
                        if (point.x != null)
                            ((IHorizonPositionObject)point.node).XGrid = point.x;
                    }
                }
            }

            return copies.Where(x => x != null).ToArray();
        }

        public static IEnumerable<OngekiObjectBase> Enumerate(OngekiObjectBase root)
            => root.EnumerateSelfAndComponents();

        public static OngekiObjectBase Clone(OngekiObjectBase source)
        {
            if (source == null)
                return null;
            var sourceNodes = Enumerate(source).ToArray();
            var originalCoordinates = sourceNodes.Select(node => (
                t: (node as ITimelineObject)?.TGrid?.CopyNew(),
                x: (node as IHorizonPositionObject)?.XGrid?.CopyNew())).ToArray();
            var copy = source.CopyNew();
            if (copy == null)
                return null;
            copy.Tag = source.Tag;
            switch (copy)
            {
                case ConnectableStartObject start:
                    start.CopyEntireConnectableObject((ConnectableStartObject)source);
                    start.RecordId = -1;
                    break;
                case Hold hold:
                    hold.CopyEntire((Hold)source);
                    hold.IsCritical = ((Hold)source).IsCritical;
                    hold.ReferenceLaneStart = ((Hold)source).ReferenceLaneStart;
                    break;
                case Soflan speed:
                    speed.CopyEntire((Soflan)source);
                    speed.SoflanGroup = ((Soflan)source).SoflanGroup;
                    break;
                case LaneBlockArea block:
                    block.CopyEntire((LaneBlockArea)source);
                    break;
                case IndividualSoflanArea area:
                    var from = (IndividualSoflanArea)source;
                    area.SoflanGroup = from.SoflanGroup;
                    area.EndIndicator.TGrid = from.EndIndicator.TGrid.CopyNew();
                    area.EndIndicator.XGrid = from.EndIndicator.XGrid.CopyNew();
                    break;
            }

            if (copy is OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase laneCopy)
                laneCopy.IsTransparent = ((OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase)source).IsTransparent;
            // CopyNew 的基础实现共享 Grid，必须在快照边界断开所有子物件的引用。
            var copiedNodes = Enumerate(copy).ToArray();
            for (var i = 0; i < copiedNodes.Length; i++)
            {
                var node = copiedNodes[i];
                node.Tag = sourceNodes[i].Tag;
                var position = originalCoordinates[i];
                if (node is ITimelineObject t && position.t != null)
                    t.TGrid = position.t.CopyNew();
                if (node is IHorizonPositionObject x && position.x != null)
                    x.XGrid = position.x.CopyNew();
            }

            if (copy is IBulletPalleteReferencable bullet && bullet.ReferenceBulletPallete != null)
                bullet.ReferenceBulletPallete = (BulletPallete)bullet.ReferenceBulletPallete.CopyNew();
            return copy;
        }

        public static (XGrid x, TGrid t) ReferencePoint(IEnumerable<OngekiObjectBase> roots)
        {
            var rootArray = roots.ToArray();
            var nodes = rootArray.SelectMany(Enumerate).Distinct().ToArray();
            var xs = nodes.OfType<IHorizonPositionObject>().Select(x => x.XGrid.TotalGrid).ToArray();

            var isaCenters = rootArray.OfType<IndividualSoflanArea>()
                .Select(area => ((long)area.TGrid.TotalGrid + area.EndIndicator.TGrid.TotalGrid) / 2d)
                .ToArray();
            var ts = isaCenters.Length > 0
                ? nodes.OfType<ITimelineObject>()
                    .Where(x => x is not IndividualSoflanArea and not IndividualSoflanArea.IndividualSoflanAreaEndIndicator && x.TGrid != null)
                    .Select(x => (double)x.TGrid.TotalGrid)
                    .Concat(isaCenters)
                    .ToArray()
                : nodes.OfType<ITimelineObject>().Where(x => x.TGrid != null).Select(x => (double)x.TGrid.TotalGrid).ToArray();

            return (XGrid.FromTotalGrid(xs.Length == 0 ? 0 : (int)Math.Round(((long)xs.Min() + xs.Max()) / 2d)),
                TGrid.FromTotalGrid(ts.Length == 0 ? 0 : (int)Math.Round(isaCenters.Length > 0 ? (ts.Min() + ts.Max()) / 2d : ts.Min())));
        }

        public static bool Place(IEnumerable<OngekiObjectBase> roots, XGrid targetX, TGrid targetT, PasteOption option)
        {
            var rootArray = roots.ToArray();
            var nodes = rootArray.SelectMany(Enumerate).Distinct().ToArray();
            // 平移不应通过持续物件 setter 静默修复原始倒置范围。
            foreach (var root in rootArray)
            {
                var span = Enumerate(root).OfType<ITimelineObject>().ToArray();
                if (root is Hold or Soflan or LaneBlockArea or IndividualSoflanArea &&
                    span.Length == 2 && span[1].TGrid < span[0].TGrid)
                    return false;
            }

            var anchor = ReferencePoint(rootArray);
            var maxT = nodes.OfType<ITimelineObject>().Where(x => x.TGrid != null).Select(x => x.TGrid.TotalGrid).DefaultIfEmpty(0).Max();
            var xs = nodes.OfType<IHorizonPositionObject>().Select(x => (long)x.XGrid.TotalGrid).ToArray();
            var mirrorXSum = xs.Length == 0 ? 0 : xs.Min() + xs.Max();
            var dx = option == PasteOption.Direct || option == PasteOption.XGridZeroMirror ? 0L : (long)targetX.TotalGrid - anchor.x.TotalGrid;
            var dt = option == PasteOption.Direct ? 0L : (long)targetT.TotalGrid - anchor.t.TotalGrid;
            var pending = nodes.Select(node => (node,
                t: node is ITimelineObject t && t.TGrid != null ? (long?)(option == PasteOption.SelectedRangeCenterTGridMirror ? (long)maxT + anchor.t.TotalGrid - t.TGrid.TotalGrid + dt : t.TGrid.TotalGrid + dt) : null,
                x: node is IHorizonPositionObject x ? (long?)(option == PasteOption.XGridZeroMirror ? -(long)x.XGrid.TotalGrid : option == PasteOption.SelectedRangeCenterXGridMirror ? mirrorXSum - x.XGrid.TotalGrid + dx : x.XGrid.TotalGrid + dx) : null)).ToArray();
            // 整组预检，禁止溢出或逐点钳零破坏物件间的相对坐标。
            if (pending.Any(v => v.t.HasValue && (v.t.Value < 0 || v.t.Value > int.MaxValue) ||
                v.x.HasValue && (v.x.Value < int.MinValue || v.x.Value > int.MaxValue)))
                return false;
            var values = pending.Select(v => (v.node, t: (int?)v.t, x: (int?)v.x)).ToArray();
            foreach (var value in values)
            {
                if (value.t is int t)
                    ((ITimelineObject)value.node).TGrid = TGrid.FromTotalGrid(t);
                if (value.x is int x)
                    ((IHorizonPositionObject)value.node).XGrid = XGrid.FromTotalGrid(x);
            }

            if (option == PasteOption.SelectedRangeCenterTGridMirror)
            {
                // 持续物件仍以较早端作为头，避免端点属性的最小时间约束截断镜像。
                foreach (var root in rootArray)
                {
                    var span = Enumerate(root).Where(x => x is ITimelineObject).ToArray();
                    if (root is ConnectableStartObject lane)
                    {
                        var children = lane.Children.ToArray();
                        var points = children.Cast<OngekiObjectBase>().Prepend(lane).ToArray();
                        var mirrored = points.Select(p => values.Single(v => ReferenceEquals(v.node, p))).Reverse().ToArray();
                        var controls = children.Select(child => child.PathControls.ToArray()).Reverse().ToArray();
                        foreach (var child in children)
                            foreach (var control in child.PathControls.ToArray())
                                child.RemoveControlObject(control);
                        for (var i = 0; i < points.Length; i++)
                        {
                            ((ITimelineObject)points[i]).TGrid = TGrid.FromTotalGrid(mirrored[i].t.Value);
                            ((IHorizonPositionObject)points[i]).XGrid = XGrid.FromTotalGrid(mirrored[i].x.Value);
                            if (i > 0)
                                foreach (var control in controls[i - 1].Reverse())
                                    children[i - 1].AddControlObject(control);
                        }
                    }
                    else if (span.Length == 2)
                    {
                        var head = values.Single(x => ReferenceEquals(x.node, span[0]));
                        var tail = values.Single(x => ReferenceEquals(x.node, span[1]));
                        if (head.t > tail.t)
                        {
                            ((ITimelineObject)span[0]).TGrid = TGrid.FromTotalGrid(tail.t.Value);
                            ((ITimelineObject)span[1]).TGrid = TGrid.FromTotalGrid(head.t.Value);
                            if (head.x.HasValue && tail.x.HasValue)
                            {
                                ((IHorizonPositionObject)span[0]).XGrid = XGrid.FromTotalGrid(tail.x.Value);
                                ((IHorizonPositionObject)span[1]).XGrid = XGrid.FromTotalGrid(head.x.Value);
                            }
                        }
                    }
                }
            }

            return true;
        }
    }
}
