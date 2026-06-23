using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private Vector4 playFieldForegroundColor;
        private bool enablePlayFieldDrawing;
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

        private sealed class FieldAreaFrameContext : IDisposable
        {
            public FieldAreaFrameContext(IFumenEditorDrawingContext target, SoflanList soflanGroup, TGrid minTGrid, TGrid maxTGrid)
            {
                Target = target;
                SoflanGroup = soflanGroup;
                MinTotalTGrid = minTGrid.TotalGrid;
                MaxTotalTGrid = maxTGrid.TotalGrid;
                LeftWallCandidates = ObjectPool.GetPooledList<LaneStartBase>();
                RightWallCandidates = ObjectPool.GetPooledList<LaneStartBase>();

                foreach (var lane in target.Editor.Fumen.Lanes.GetVisibleStartObjects(minTGrid, maxTGrid))
                {
                    switch (lane.LaneType)
                    {
                        case LaneType.WallLeft:
                            LeftWallCandidates.Add(lane);
                            break;
                        case LaneType.WallRight:
                            RightWallCandidates.Add(lane);
                            break;
                    }
                }
            }

            public IFumenEditorDrawingContext Target { get; }
            public SoflanList SoflanGroup { get; }
            public int MinTotalTGrid { get; }
            public int MaxTotalTGrid { get; }
            public IPooledList<LaneStartBase> LeftWallCandidates { get; }
            public IPooledList<LaneStartBase> RightWallCandidates { get; }

            public void Dispose()
            {
                LeftWallCandidates.Dispose();
                RightWallCandidates.Dispose();
            }
        }

        public void Initalize(IRenderManagerImpl impl)
        {
            UpdateProps();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        private void Default_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.EditorGlobalSetting.EnablePlayFieldDrawing):
                case nameof(Properties.EditorGlobalSetting.PlayFieldForegroundColor):
                    UpdateProps();
                    break;
                default:
                    break;
            }
        }

        private void UpdateProps()
        {
            enablePlayFieldDrawing = Properties.EditorGlobalSetting.Default.EnablePlayFieldDrawing;
            playFieldForegroundColor = Color.FromArgb(Properties.EditorGlobalSetting.Default.PlayFieldForegroundColor).ToVector4();
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
            if (target.Editor.IsDesignMode || !enablePlayFieldDrawing)
                return;

            if (fieldMinTGrid is null || fieldMaxTGrid is null || fieldMaxTGrid < fieldMinTGrid)
                return;

            using var frameContext = new FieldAreaFrameContext(
                target,
                target.CurrentDrawingTargetContext.CurrentSoflanList,
                fieldMinTGrid,
                fieldMaxTGrid);

            using var sampleTGrids = ObjectPool.GetPooledSet<int>();
            CollectBaseSampleTGrids(frameContext, sampleTGrids);

            if (sampleTGrids.Count < 2)
                return;

            using var sortedTGrids = ObjectPool.GetPooledList<int>();
            sortedTGrids.AddRange(sampleTGrids);
            sortedTGrids.Sort(Comparer<int>.Default);
            AddScreenDistanceSamples(target, frameContext.SoflanGroup, sortedTGrids);

            using var limitParams = ObjectPool.GetPooledList<FieldLimitParam>();
            for (var i = 0; i < sortedTGrids.Count; i++)
            {
                var sample = BuildAreaSample(frameContext, sortedTGrids[i]);
                limitParams.Add(ConvertToLimitParam(target, frameContext.SoflanGroup, sample));
            }

            DrawFieldQuads(builder, limitParams, playFieldForegroundColor);

            DebugDrawSamples(target, builder, limitParams);
        }

        private static void CollectBaseSampleTGrids(FieldAreaFrameContext context, ISet<int> result)
        {
            var minTotalTGrid = context.MinTotalTGrid;
            var maxTotalTGrid = context.MaxTotalTGrid;
            int? prevContextTotalTGrid = null;
            int? nextContextTotalTGrid = null;

            AddSampleOrContext(
                minTotalTGrid,
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);
            AddSampleOrContext(
                maxTotalTGrid,
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);

            var currentTGrid = context.Target.Editor.GetViewportTGrid();
            if (currentTGrid is not null)
                AddSampleOrContext(
                    currentTGrid.TotalGrid,
                    minTotalTGrid,
                    maxTotalTGrid,
                    result,
                    ref prevContextTotalTGrid,
                    ref nextContextTotalTGrid);

            AddWallCandidateSamples(
                context.LeftWallCandidates,
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);
            AddWallCandidateSamples(
                context.RightWallCandidates,
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);

            AddSoflanSamples(
                context.SoflanGroup.GetCachedSoflanPositionList_PreviewMode(context.Target.Editor.Fumen.BpmList),
                minTotalTGrid,
                maxTotalTGrid,
                result,
                ref prevContextTotalTGrid,
                ref nextContextTotalTGrid);

            if (prevContextTotalTGrid.HasValue)
                result.Add(prevContextTotalTGrid.Value);
            if (nextContextTotalTGrid.HasValue)
                result.Add(nextContextTotalTGrid.Value);
        }

        private static void AddWallCandidateSamples(
            IReadOnlyList<LaneStartBase> candidates,
            int minTotalTGrid,
            int maxTotalTGrid,
            ISet<int> result,
            ref int? prevContextTotalTGrid,
            ref int? nextContextTotalTGrid)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var lane = candidates[i];
                AddSampleOrContext(lane.MinTGrid.TotalGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);
                AddSampleOrContext(lane.MaxTGrid.TotalGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);
                AddSampleOrContext(lane.TGrid.TotalGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);

                foreach (var child in lane.Children)
                    AddSampleOrContext(child.TGrid.TotalGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);
            }
        }

        private static void AddSoflanSamples(
            IList<SoflanList.SoflanPoint> soflanPoints,
            int minTotalTGrid,
            int maxTotalTGrid,
            ISet<int> result,
            ref int? prevContextTotalTGrid,
            ref int? nextContextTotalTGrid)
        {
            var startIndex = LowerBoundSoflanPoint(soflanPoints, minTotalTGrid);
            if (startIndex > 0)
                AddSampleOrContext(soflanPoints[startIndex - 1].TGrid.TotalGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);

            for (var i = startIndex; i < soflanPoints.Count; i++)
            {
                var totalTGrid = soflanPoints[i].TGrid.TotalGrid;
                AddSampleOrContext(totalTGrid, minTotalTGrid, maxTotalTGrid, result, ref prevContextTotalTGrid, ref nextContextTotalTGrid);

                if (totalTGrid > maxTotalTGrid)
                    break;
            }
        }

        private static int LowerBoundSoflanPoint(IList<SoflanList.SoflanPoint> soflanPoints, int totalTGrid)
        {
            var lo = 0;
            var hi = soflanPoints.Count;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (soflanPoints[mid].TGrid.TotalGrid < totalTGrid)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private static void AddSampleOrContext(
            int totalTGrid,
            int minTotalTGrid,
            int maxTotalTGrid,
            ISet<int> result,
            ref int? prevContextTotalTGrid,
            ref int? nextContextTotalTGrid)
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

        private static FieldAreaSample BuildAreaSample(FieldAreaFrameContext context, int totalTGrid)
        {
            var tGrid = TGrid.FromTotalGrid(totalTGrid);
            var left = QueryBoundaryXGridUnit(context.LeftWallCandidates, LaneType.WallLeft, tGrid) ?? new(DefaultLeftXGridUnit, DefaultLeftXGridUnit);
            var right = QueryBoundaryXGridUnit(context.RightWallCandidates, LaneType.WallRight, tGrid) ?? new(DefaultRightXGridUnit, DefaultRightXGridUnit);
            var isValid = IsFinite(left.Bef) && IsFinite(left.Aft) && IsFinite(right.Bef) && IsFinite(right.Aft);

            return new(totalTGrid, left.Bef, left.Aft, right.Bef, right.Aft, isValid);
        }

        private static BoundarySample? QueryBoundaryXGridUnit(IReadOnlyList<LaneStartBase> candidates, LaneType laneType, TGrid tGrid)
        {
            double? bef = null;
            double? aft = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var lane = candidates[i];
                if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Bef)
                    && CalculateBoundaryXGridUnit(lane, tGrid, BoundaryEdge.Bef) is double befValue)
                {
                    bef = MergeBoundary(laneType, bef, befValue);
                }

                if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Aft)
                    && CalculateBoundaryXGridUnit(lane, tGrid, BoundaryEdge.Aft) is double aftValue)
                {
                    aft = MergeBoundary(laneType, aft, aftValue);
                }
            }

            if (!bef.HasValue && !aft.HasValue)
                return null;

            var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
            return new(bef ?? defaultValue, aft ?? defaultValue);
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
            var children = lane.GetChildObjectsFromTGrid(tGrid);
            var isPathValid = lane.IsPathVaild();
            var childCount = 0;
            ConnectableChildObjectBase firstChild = null;
            ConnectableChildObjectBase firstExactChild = null;
            ConnectableChildObjectBase lastExactChild = null;
            double? bestValue = null;

            foreach (var child in children)
            {
                childCount++;
                firstChild ??= child;

                if (child.TGrid.TotalGrid == tGrid.TotalGrid)
                {
                    firstExactChild ??= child;
                    lastExactChild = child;
                }

                if (!isPathValid && child.CalulateXGrid(tGrid)?.TotalUnit is double childValue)
                    bestValue = MergeBoundary(lane.LaneType, bestValue, childValue);
            }

            if (childCount == 0)
            {
                var x = lane.CalulateXGrid(tGrid)?.TotalUnit ?? lane.XGrid?.TotalUnit ?? double.NaN;
                return double.IsNaN(x) ? null : x;
            }

            if (firstExactChild is not null && lastExactChild is not null)
            {
                var child = edge == BoundaryEdge.Bef ? firstExactChild : lastExactChild;
                return child.XGrid.TotalUnit;
            }

            if (isPathValid)
                return firstChild?.CalulateXGrid(tGrid)?.TotalUnit;

            return bestValue;
        }

        private static double MergeBoundary(LaneType laneType, double currentValue, double newValue)
        {
            return laneType == LaneType.WallLeft
                ? Math.Min(currentValue, newValue)
                : Math.Max(currentValue, newValue);
        }

        private static double MergeBoundary(LaneType laneType, double? currentValue, double newValue)
        {
            return currentValue.HasValue
                ? MergeBoundary(laneType, currentValue.Value, newValue)
                : newValue;
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

            var isValid = IsFinite(xBefL) && IsFinite(xAftL) && IsFinite(xBefR) && IsFinite(xAftR);
            var normalizedBef = NormalizeBoundaryPair(xBefL, xBefR);
            var normalizedAft = NormalizeBoundaryPair(xAftL, xAftR);

            return new(
                sample.TotalTGrid,
                (float)normalizedBef.Left,
                (float)normalizedAft.Left,
                (float)normalizedBef.Right,
                (float)normalizedAft.Right,
                (float)y,
                isValid);
        }

        private static (double Left, double Right) NormalizeBoundaryPair(double x1, double x2)
            => x1 <= x2 ? (x1, x2) : (x2, x1);

        private static void DrawFieldQuads(IDrawCommandListBuilder builder, IList<FieldLimitParam> limitParams, Vector4 playFieldForegroundColor)
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

                vertices.Add(new(new(cur.XAftL, cur.Y), playFieldForegroundColor));
                vertices.Add(new(new(next.XBefL, next.Y), playFieldForegroundColor));
                vertices.Add(new(new(cur.XAftR, cur.Y), playFieldForegroundColor));

                vertices.Add(new(new(cur.XAftR, cur.Y), playFieldForegroundColor));
                vertices.Add(new(new(next.XBefL, next.Y), playFieldForegroundColor));
                vertices.Add(new(new(next.XBefR, next.Y), playFieldForegroundColor));
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
