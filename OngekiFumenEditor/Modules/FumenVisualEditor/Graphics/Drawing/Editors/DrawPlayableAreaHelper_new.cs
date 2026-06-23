using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawPlayableAreaHelper_new
    {
        private const double DefaultLeftXGridUnit = -24;
        private const double DefaultRightXGridUnit = 24;
        private const double MaxSampleScreenDistance = 32;
        private const int MaxExtraSamplesPerSegment = 64;
        private const double MinQuadScreenHeight = 0.001;
        private static readonly Vector4 PlayFieldForegroundColor = new(0.25f, 0.25f, 0, 1);
        private readonly LineVertex[] vertices = new LineVertex[2];

        private readonly record struct FieldAreaSample(
            int TotalTGrid,
            double PlaceBefL,
            double PlaceAftL,
            double PlaceBefR,
            double PlaceAftR,
            bool IsValid);

        private readonly record struct FieldLimitParam(
            int TotalTGrid,
            float XBefL,
            float XAftL,
            float XBefR,
            float XAftR,
            float Y,
            bool IsValid);

        private enum BoundaryEdge
        {
            Bef,
            Aft
        }

        private readonly record struct BoundarySample(double Bef, double Aft);

        public void Initalize(IRenderManagerImpl impl)
        {
        }

        public void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            if (target.Editor.IsDesignMode)
                DrawAudioDuration(target, builder);
        }

        private void DrawAudioDuration(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            var y = (float)(target.Editor.TotalDurationHeight - target.CurrentDrawingTargetContext.ViewRelativeOriginY);

            var color = new Vector4(1, 0, 0, 1);
            vertices[0] = new(new(0, y), color, VertexDash.Solider);
            vertices[1] = new(new(target.CurrentDrawingTargetContext.ViewRelativeRect.Width, y), color, VertexDash.Solider);

            builder.DrawSimpleLines(vertices, 3);
        }

        public void DrawPlayField(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, TGrid fieldMinTGrid, TGrid fieldMaxTGrid)
        {
            if (target.Editor.IsDesignMode)
                return;

            if (fieldMinTGrid is null || fieldMaxTGrid is null || fieldMaxTGrid < fieldMinTGrid)
                return;

            var soflanGroup = target.CurrentDrawingTargetContext.CurrentSoflanList;
            using var sampleTGrids = ObjectPool.GetPooledSet<int>();
            CollectBaseSampleTGrids(target, fieldMinTGrid, fieldMaxTGrid, soflanGroup, sampleTGrids);

            if (sampleTGrids.Count < 2)
                return;

            using var sortedTGrids = ObjectPool.GetPooledList<int>();
            sortedTGrids.AddRange(sampleTGrids);
            sortedTGrids.Sort(Comparer<int>.Default);
            AddScreenDistanceSamples(target, soflanGroup, sortedTGrids);

            using var limitParams = ObjectPool.GetPooledList<FieldLimitParam>();
            for (var i = 0; i < sortedTGrids.Count; i++)
            {
                var sample = BuildAreaSample(target, sortedTGrids[i]);
                limitParams.Add(ConvertToLimitParam(target, soflanGroup, sample));
            }

            DrawFieldQuads(builder, limitParams);

            DebugDrawSamples(target, builder, limitParams);
        }

        private static void CollectBaseSampleTGrids(IFumenEditorDrawingContext target, TGrid minTGrid, TGrid maxTGrid, SoflanList soflanGroup, ISet<int> result)
        {
            var minTotalTGrid = minTGrid.TotalGrid;
            var maxTotalTGrid = maxTGrid.TotalGrid;
            int? prevContextTotalTGrid = null;
            int? nextContextTotalTGrid = null;

            void addSampleOrContext(int totalTGrid)
            {
                if (minTotalTGrid <= totalTGrid && totalTGrid <= maxTotalTGrid)
                {
                    result.Add(totalTGrid);
                    return;
                }

                if (totalTGrid < minTotalTGrid)
                    prevContextTotalTGrid = !prevContextTotalTGrid.HasValue || totalTGrid > prevContextTotalTGrid.Value
                        ? totalTGrid
                        : prevContextTotalTGrid;
                else
                    nextContextTotalTGrid = !nextContextTotalTGrid.HasValue || totalTGrid < nextContextTotalTGrid.Value
                        ? totalTGrid
                        : nextContextTotalTGrid;
            }

            addSampleOrContext(minTotalTGrid);
            addSampleOrContext(maxTotalTGrid);

            var currentTGrid = target.Editor.GetViewportTGrid();
            if (currentTGrid is not null)
                addSampleOrContext(currentTGrid.TotalGrid);

            var fumen = target.Editor.Fumen;
            foreach (var lane in fumen.Lanes.Where(x => x.LaneType is LaneType.WallLeft or LaneType.WallRight))
            {
                addSampleOrContext(lane.MinTGrid.TotalGrid);
                addSampleOrContext(lane.MaxTGrid.TotalGrid);
                addSampleOrContext(lane.TGrid.TotalGrid);

                foreach (var child in lane.Children)
                    addSampleOrContext(child.TGrid.TotalGrid);
            }

            foreach (var point in soflanGroup.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList))
                addSampleOrContext(point.TGrid.TotalGrid);

            if (prevContextTotalTGrid.HasValue)
                result.Add(prevContextTotalTGrid.Value);
            if (nextContextTotalTGrid.HasValue)
                result.Add(nextContextTotalTGrid.Value);
        }

        private static void AddScreenDistanceSamples(IFumenEditorDrawingContext target, SoflanList soflanGroup, IList<int> sortedTGrids)
        {
            var i = 0;
            while (i < sortedTGrids.Count - 1)
            {
                var from = sortedTGrids[i];
                var to = sortedTGrids[i + 1];
                if (from == to)
                {
                    sortedTGrids.RemoveAt(i + 1);
                    continue;
                }

                var fromY = target.ConvertToViewRelativeY(TGrid.FromTotalGrid(from), soflanGroup);
                var toY = target.ConvertToViewRelativeY(TGrid.FromTotalGrid(to), soflanGroup);
                if (!IsFinite(fromY) || !IsFinite(toY))
                {
                    i++;
                    continue;
                }

                var distance = Math.Abs(toY - fromY);
                if (distance <= MaxSampleScreenDistance)
                {
                    i++;
                    continue;
                }

                var extraCount = Math.Min(MaxExtraSamplesPerSegment, (int)Math.Ceiling(distance / MaxSampleScreenDistance) - 1);
                if (extraCount <= 0)
                {
                    i++;
                    continue;
                }

                var insertCount = 0;
                var lastInserted = int.MinValue;
                for (var s = 1; s <= extraCount; s++)
                {
                    var totalGrid = from + (int)Math.Round((to - from) * (s / (extraCount + 1d)));
                    if (from < totalGrid && totalGrid < to && totalGrid != lastInserted)
                    {
                        sortedTGrids.Insert(i + 1 + insertCount, totalGrid);
                        insertCount++;
                        lastInserted = totalGrid;
                    }
                }

                i += insertCount + 1;
            }
        }

        private static FieldAreaSample BuildAreaSample(IFumenEditorDrawingContext target, int totalTGrid)
        {
            var tGrid = TGrid.FromTotalGrid(totalTGrid);
            var left = QueryBoundaryXGridUnit(target, LaneType.WallLeft, tGrid) ?? new(DefaultLeftXGridUnit, DefaultLeftXGridUnit);
            var right = QueryBoundaryXGridUnit(target, LaneType.WallRight, tGrid) ?? new(DefaultRightXGridUnit, DefaultRightXGridUnit);
            var isValid = IsFinite(left.Bef) && IsFinite(left.Aft) && IsFinite(right.Bef) && IsFinite(right.Aft)
                && left.Bef <= right.Bef
                && left.Aft <= right.Aft;

            return new(totalTGrid, left.Bef, left.Aft, right.Bef, right.Aft, isValid);
        }

        private static BoundarySample? QueryBoundaryXGridUnit(IFumenEditorDrawingContext target, LaneType laneType, TGrid tGrid)
        {
            var bef = QueryBoundaryXGridUnit(target, laneType, tGrid, BoundaryEdge.Bef);
            var aft = QueryBoundaryXGridUnit(target, laneType, tGrid, BoundaryEdge.Aft);
            if (!bef.HasValue && !aft.HasValue)
                return null;

            var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
            return new(bef ?? defaultValue, aft ?? defaultValue);
        }

        private static double? QueryBoundaryXGridUnit(IFumenEditorDrawingContext target, LaneType laneType, TGrid tGrid, BoundaryEdge edge)
        {
            var candidates = target.Editor.Fumen.Lanes
                .GetVisibleStartObjects(tGrid, tGrid)
                .Where(x => x.LaneType == laneType)
                .Where(x => IsActiveAtBoundaryEdge(x, tGrid, edge))
                .Select(x => CalculateBoundaryXGridUnit(x, tGrid, edge))
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToArray();

            if (candidates.Length == 0)
                return null;

            if (laneType == LaneType.WallLeft)
                return candidates.Min();

            return candidates.Max();
        }

        private static bool IsActiveAtBoundaryEdge(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
        {
            var totalGrid = tGrid.TotalGrid;
            return edge == BoundaryEdge.Bef
                ? lane.MinTGrid.TotalGrid < totalGrid && totalGrid <= lane.MaxTGrid.TotalGrid
                : lane.MinTGrid.TotalGrid <= totalGrid && totalGrid < lane.MaxTGrid.TotalGrid;
        }

        private static double? CalculateBoundaryXGridUnit(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
        {
            var children = lane.GetChildObjectsFromTGrid(tGrid).ToArray();
            if (children.Length == 0)
            {
                var x = lane.CalulateXGrid(tGrid)?.TotalUnit ?? lane.XGrid?.TotalUnit ?? double.NaN;
                return double.IsNaN(x) ? null : x;
            }

            var exactChildren = children
                .Where(x => x.TGrid.TotalGrid == tGrid.TotalGrid)
                .ToArray();
            if (exactChildren.Length > 0)
            {
                return edge == BoundaryEdge.Bef
                    ? exactChildren.First().XGrid.TotalUnit
                    : exactChildren.Last().XGrid.TotalUnit;
            }

            if (lane.IsPathVaild())
                return children.FirstOrDefault()?.CalulateXGrid(tGrid)?.TotalUnit;

            var values = children
                .Select(x => x.CalulateXGrid(tGrid)?.TotalUnit)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToArray();

            if (values.Length == 0)
                return null;

            return lane.LaneType == LaneType.WallLeft
                ? values.Min()
                : values.Max();
        }

        private static FieldLimitParam ConvertToLimitParam(IFumenEditorDrawingContext target, SoflanList soflanGroup, FieldAreaSample sample)
        {
            var y = target.ConvertToViewRelativeY(TGrid.FromTotalGrid(sample.TotalTGrid), soflanGroup);
            if (!sample.IsValid || !IsFinite(y))
                return new(sample.TotalTGrid, 0, 0, 0, 0, 0, false);

            var xBefL = XGridCalculator.ConvertXGridToX(sample.PlaceBefL, target.Editor);
            var xAftL = XGridCalculator.ConvertXGridToX(sample.PlaceAftL, target.Editor);
            var xBefR = XGridCalculator.ConvertXGridToX(sample.PlaceBefR, target.Editor);
            var xAftR = XGridCalculator.ConvertXGridToX(sample.PlaceAftR, target.Editor);

            var isValid = IsFinite(xBefL) && IsFinite(xAftL) && IsFinite(xBefR) && IsFinite(xAftR)
                && xBefL <= xBefR
                && xAftL <= xAftR;

            return new(
                sample.TotalTGrid,
                (float)xBefL,
                (float)xAftL,
                (float)xBefR,
                (float)xAftR,
                (float)y,
                isValid);
        }

        private static void DrawFieldQuads(IDrawCommandListBuilder builder, IList<FieldLimitParam> limitParams)
        {
            using var vertices = ObjectPool.GetPooledList<PolygonVertex>();
            for (var i = 0; i < limitParams.Count - 1; i++)
            {
                var cur = limitParams[i];
                var next = limitParams[i + 1];
                if (!cur.IsValid || !next.IsValid)
                    continue;

                if (Math.Abs(next.Y - cur.Y) < MinQuadScreenHeight)
                    continue;

                if (cur.XAftL > cur.XAftR || next.XBefL > next.XBefR)
                    continue;

                vertices.Add(new(new(cur.XAftL, cur.Y), PlayFieldForegroundColor));
                vertices.Add(new(new(next.XBefL, next.Y), PlayFieldForegroundColor));
                vertices.Add(new(new(cur.XAftR, cur.Y), PlayFieldForegroundColor));

                vertices.Add(new(new(cur.XAftR, cur.Y), PlayFieldForegroundColor));
                vertices.Add(new(new(next.XBefL, next.Y), PlayFieldForegroundColor));
                vertices.Add(new(new(next.XBefR, next.Y), PlayFieldForegroundColor));
            }

            if (vertices.Count > 0)
                builder.DrawPolygon(Primitive.Triangles, vertices);
        }

        private static void AddTGrid(ISet<int> result, int totalTGrid, int minTotalTGrid, int maxTotalTGrid)
        {
            if (minTotalTGrid <= totalTGrid && totalTGrid <= maxTotalTGrid)
                result.Add(totalTGrid);
        }

        private static bool IsFinite(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        [System.Diagnostics.Conditional("PLAYFIELD_DEBUG")]
        private static void DebugDrawSamples(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IList<FieldLimitParam> limitParams)
        {
            var validColor = new Vector4(1, 1, 0, 0.75f);
            var invalidColor = new Vector4(1, 0, 0, 0.75f);
            builder.DrawCircles(limitParams.SelectMany(x => new[]
            {
                new CircleInstance(new(x.XAftL, x.Y), x.IsValid ? validColor : invalidColor, false, 6, 0),
                new CircleInstance(new(x.XAftR, x.Y), x.IsValid ? validColor : invalidColor, false, 6, 0),
            }));
        }
    }
}
