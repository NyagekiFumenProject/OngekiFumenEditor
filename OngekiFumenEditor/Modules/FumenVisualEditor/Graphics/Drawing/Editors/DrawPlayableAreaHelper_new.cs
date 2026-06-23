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

        // 中间采样点到合并后边界线的最大允许屏幕距离。
        private const double MergeCollinearScreenDistance = 0.5;

        // 过薄的四边形没有可见意义，也容易产生退化三角形。
        private const double MinQuadScreenHeight = 0.001;
        private Vector4 playFieldForegroundColor;
        private bool enablePlayFieldDrawing;
        private readonly LineVertex[] vertices = new LineVertex[2];

        /// <summary>
        /// 谱面坐标中的一个时间采样截面。
        /// Prev 用来连接上一段，Next 用来连接下一段。
        /// </summary>
        private readonly record struct PlayFieldAreaSample(
            int TotalTGrid,
            double PlacePrevL,
            double PlaceNextL,
            double PlacePrevR,
            double PlaceNextR,
            bool IsValid);

        /// <summary>
        /// 转换到屏幕坐标后的采样截面。
        /// </summary>
        private readonly record struct PlayFieldLimitParam(
            int TotalTGrid,
            float XPrevL,
            float XNextL,
            float XPrevR,
            float XNextR,
            float Y,
            bool IsValid);

        private enum BoundaryEdge
        {
            Prev,
            Next
        }

        private readonly record struct BoundarySample(double Prev, double Next);

        /// <summary>
        /// 单次绘制内共享的上下文，缓存本次可见范围内可能用到的墙轨。
        /// </summary>
        private sealed class FieldAreaFrameContext : IDisposable
        {
            /// <summary>
            /// 创建本次绘制用的上下文，并缓存可见范围内的左右墙轨。
            /// </summary>
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

            /// <summary>
            /// 释放本次绘制租用的候选墙轨列表。
            /// </summary>
            public void Dispose()
            {
                LeftWallCandidates.Dispose();
                RightWallCandidates.Dispose();
            }
        }

        /// <summary>
        /// 初始化设置缓存，并监听可击打区域相关设置变化。
        /// </summary>
        public void Initalize(IRenderManagerImpl impl)
        {
            UpdateProps();
            Properties.EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged;
        }

        /// <summary>
        /// 响应全局设置变化，刷新绘制开关和前景色。
        /// </summary>
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

        /// <summary>
        /// 从全局设置读取当前可击打区域绘制参数。
        /// </summary>
        private void UpdateProps()
        {
            enablePlayFieldDrawing = Properties.EditorGlobalSetting.Default.EnablePlayFieldDrawing;
            playFieldForegroundColor = Color.FromArgb(Properties.EditorGlobalSetting.Default.PlayFieldForegroundColor).ToVector4();
        }

        /// <summary>
        /// 绘制 helper 的设计模式内容。
        /// </summary>
        public void Draw(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            if (target.Editor.IsDesignMode)
                DrawAudioDuration(target, builder);
        }

        /// <summary>
        /// 在设计模式绘制音频结束线。
        /// </summary>
        private void DrawAudioDuration(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder)
        {
            var y = (float)(target.Editor.TotalDurationHeight - target.CurrentDrawingTargetContext.ViewRelativeOriginY);

            var color = new Vector4(1, 0, 0, 1);
            vertices[0] = new(new(0, y), color, VertexDash.Solider);
            vertices[1] = new(new(target.CurrentDrawingTargetContext.ViewRelativeRect.Width, y), color, VertexDash.Solider);

            builder.DrawSimpleLines(vertices, 3);
        }

        /// <summary>
        /// 在预览模式绘制当前可见时间范围内的可击打区域。
        /// </summary>
        public void DrawPlayField(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, TGrid fieldMinTGrid, TGrid fieldMaxTGrid)
        {
            if (target.Editor.IsDesignMode || !enablePlayFieldDrawing)
                return;

            if (fieldMinTGrid is null || fieldMaxTGrid is null || fieldMaxTGrid < fieldMinTGrid)
                return;

            // 主流程：收集采样点，计算每个截面的左右边界，再连接相邻截面。
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

            using var limitParams = ObjectPool.GetPooledList<PlayFieldLimitParam>();
            for (var i = 0; i < sortedTGrids.Count; i++)
            {
                var sample = BuildAreaSample(frameContext, sortedTGrids[i]);
                AddMergedLimitParam(
                    limitParams,
                    ConvertToLimitParam(target, frameContext.SoflanGroup, sample),
                    fieldMinTGrid.TotalGrid,
                    fieldMaxTGrid.TotalGrid);
            }

            DrawFieldQuads(
                builder,
                limitParams,
                playFieldForegroundColor,
                fieldMinTGrid.TotalGrid,
                fieldMaxTGrid.TotalGrid,
                target.CurrentDrawingTargetContext.ViewRelativeRect.MinY,
                target.CurrentDrawingTargetContext.ViewRelativeRect.MaxY);

            DebugDrawSamples(target, builder, limitParams);
        }

        /// <summary>
        /// 收集用于计算区域边界的基础时间采样点。
        /// </summary>
        private static void CollectBaseSampleTGrids(FieldAreaFrameContext context, ISet<int> result)
        {
            // 采样点必须覆盖可见边界、当前视口、墙轨节点和 Soflan 节点。
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

        /// <summary>
        /// 把墙轨起点、终点和子节点时间加入采样集合。
        /// </summary>
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

        /// <summary>
        /// 把 Soflan 节点时间加入采样集合。
        /// </summary>
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

        /// <summary>
        /// 在 Soflan 节点列表中查找第一个不小于指定时间的位置。
        /// </summary>
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

        /// <summary>
        /// 添加范围内采样点；范围外只记录最近的上下文点。
        /// </summary>
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

        /// <summary>
        /// 按屏幕距离在采样点之间补点，减少变速造成的形状误差。
        /// </summary>
        private static void AddScreenDistanceSamples(IFumenEditorDrawingContext target, SoflanList soflanGroup, IList<int> sortedTGrids)
        {
            // 时间间隔短不代表屏幕距离短，变速很快时需要按屏幕距离补采样。
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
                if (!IsValueValid(fromY) || !IsValueValid(toY))
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

        /// <summary>
        /// 构造一个时间点上的左右墙轨边界采样。
        /// </summary>
        private static PlayFieldAreaSample BuildAreaSample(FieldAreaFrameContext context, int totalTGrid)
        {
            var tGrid = TGrid.FromTotalGrid(totalTGrid);
            var left = QueryBoundaryXGridUnit(context.LeftWallCandidates, LaneType.WallLeft, tGrid) ?? new(DefaultLeftXGridUnit, DefaultLeftXGridUnit);
            var right = QueryBoundaryXGridUnit(context.RightWallCandidates, LaneType.WallRight, tGrid) ?? new(DefaultRightXGridUnit, DefaultRightXGridUnit);
            var isValid = IsValueValid(left.Prev) && IsValueValid(left.Next) && IsValueValid(right.Prev) && IsValueValid(right.Next);

            return new(totalTGrid, left.Prev, left.Next, right.Prev, right.Next, isValid);
        }

        /// <summary>
        /// 查询指定墙侧在一个时间点上的 Prev/Next 边界。
        /// </summary>
        private static BoundarySample? QueryBoundaryXGridUnit(IReadOnlyList<LaneStartBase> candidates, LaneType laneType, TGrid tGrid)
        {
            // 同一时间可能有多条墙轨，左墙取最左，右墙取最右。
            double? prev = null;
            double? next = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var lane = candidates[i];
                if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Prev)
                    && CalculateBoundaryXGridUnit(lane, tGrid, BoundaryEdge.Prev) is double prevValue)
                {
                    prev = MergeBoundary(laneType, prev, prevValue);
                }

                if (IsActiveAtBoundaryEdge(lane, tGrid, BoundaryEdge.Next)
                    && CalculateBoundaryXGridUnit(lane, tGrid, BoundaryEdge.Next) is double nextValue)
                {
                    next = MergeBoundary(laneType, next, nextValue);
                }
            }

            if (!prev.HasValue && !next.HasValue)
                return null;

            var defaultValue = laneType == LaneType.WallLeft ? DefaultLeftXGridUnit : DefaultRightXGridUnit;
            return new(prev ?? defaultValue, next ?? defaultValue);
        }

        /// <summary>
        /// 判断墙轨在指定时间点是否参与 Prev 或 Next 边界。
        /// </summary>
        private static bool IsActiveAtBoundaryEdge(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
        {
            var totalGrid = tGrid.TotalGrid;
            // Prev/Next 使用相反的开闭区间，避免在节点处重复连接同一段。
            return edge == BoundaryEdge.Prev
                ? lane.MinTGrid.TotalGrid < totalGrid && totalGrid <= lane.MaxTGrid.TotalGrid
                : lane.MinTGrid.TotalGrid <= totalGrid && totalGrid < lane.MaxTGrid.TotalGrid;
        }

        /// <summary>
        /// 计算单条墙轨在指定时间点上的边界 XGrid。
        /// </summary>
        private static double? CalculateBoundaryXGridUnit(LaneStartBase lane, TGrid tGrid, BoundaryEdge edge)
        {
            // 节点正好落在采样点上时，Prev 取前侧 child，Next 取后侧 child。
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
                var child = edge == BoundaryEdge.Prev ? firstExactChild : lastExactChild;
                return child.XGrid.TotalUnit;
            }

            if (isPathValid)
                return firstChild?.CalulateXGrid(tGrid)?.TotalUnit;

            return bestValue;
        }

        /// <summary>
        /// 按墙侧合并两个边界值，保留外侧边界。
        /// </summary>
        private static double MergeBoundary(LaneType laneType, double currentValue, double newValue)
        {
            return laneType == LaneType.WallLeft
                ? Math.Min(currentValue, newValue)
                : Math.Max(currentValue, newValue);
        }

        /// <summary>
        /// 按墙侧合并可空边界值，空值时直接使用新值。
        /// </summary>
        private static double MergeBoundary(LaneType laneType, double? currentValue, double newValue)
        {
            return currentValue.HasValue
                ? MergeBoundary(laneType, currentValue.Value, newValue)
                : newValue;
        }

        /// <summary>
        /// 把谱面坐标采样转换成屏幕坐标采样。
        /// </summary>
        private static PlayFieldLimitParam ConvertToLimitParam(IFumenEditorDrawingContext target, SoflanList soflanGroup, PlayFieldAreaSample sample)
        {
            var y = target.ConvertToViewRelativeY(TGrid.FromTotalGrid(sample.TotalTGrid), soflanGroup);
            if (!sample.IsValid || !IsValueValid(y))
                return new(sample.TotalTGrid, 0, 0, 0, 0, 0, false);

            var xPrevL = XGridCalculator.ConvertXGridToX(sample.PlacePrevL, target.Editor);
            var xNextL = XGridCalculator.ConvertXGridToX(sample.PlaceNextL, target.Editor);
            var xPrevR = XGridCalculator.ConvertXGridToX(sample.PlacePrevR, target.Editor);
            var xNextR = XGridCalculator.ConvertXGridToX(sample.PlaceNextR, target.Editor);

            var isValid = IsValueValid(xPrevL) && IsValueValid(xNextL) && IsValueValid(xPrevR) && IsValueValid(xNextR);
            var normalizedPrev = NormalizeBoundaryPair(xPrevL, xPrevR);
            var normalizedNext = NormalizeBoundaryPair(xNextL, xNextR);

            return new(
                sample.TotalTGrid,
                (float)normalizedPrev.Left,
                (float)normalizedNext.Left,
                (float)normalizedPrev.Right,
                (float)normalizedNext.Right,
                (float)y,
                isValid);
        }

        /// <summary>
        /// 把两个屏幕 X 坐标整理为实际左边界和右边界。
        /// </summary>
        private static (double Left, double Right) NormalizeBoundaryPair(double x1, double x2)
            => x1 <= x2 ? (x1, x2) : (x2, x1);

        /// <summary>
        /// 合并屏幕坐标下左右边界都近似共线的中间采样截面。
        /// </summary>
        private static void AddMergedLimitParam(
            IList<PlayFieldLimitParam> limitParams,
            PlayFieldLimitParam limitParam,
            int visibleMinTotalTGrid,
            int visibleMaxTotalTGrid)
        {
            if (limitParams.Count >= 2)
            {
                var curIndex = limitParams.Count - 1;
                var prevIndex = curIndex - 1;
                if (!IsVisibleRangeBoundary(limitParams[curIndex].TotalTGrid, visibleMinTotalTGrid, visibleMaxTotalTGrid)
                    && CanMergeLimitParam(limitParams[prevIndex], limitParams[curIndex], limitParam))
                {
                    limitParams[curIndex] = limitParam;
                    return;
                }
            }

            limitParams.Add(limitParam);
        }

        /// <summary>
        /// 当前可见范围边界不能被共线合并移除，否则上下文截面会被直接当作可见绘制范围。
        /// </summary>
        private static bool IsVisibleRangeBoundary(int totalTGrid, int visibleMinTotalTGrid, int visibleMaxTotalTGrid)
            => totalTGrid == visibleMinTotalTGrid || totalTGrid == visibleMaxTotalTGrid;

        /// <summary>
        /// 判断中间截面是否可以由前后截面直接连接替代。
        /// </summary>
        private static bool CanMergeLimitParam(PlayFieldLimitParam prev, PlayFieldLimitParam cur, PlayFieldLimitParam next)
        {
            if (!prev.IsValid || !cur.IsValid || !next.IsValid)
                return false;

            if (!IsBoundaryContinuous(cur.XPrevL, cur.XNextL) || !IsBoundaryContinuous(cur.XPrevR, cur.XNextR))
                return false;

            var curLeftX = (cur.XPrevL + cur.XNextL) * 0.5;
            var curRightX = (cur.XPrevR + cur.XNextR) * 0.5;
            var leftDistance = DistancePointToLine(
                curLeftX,
                cur.Y,
                prev.XNextL,
                prev.Y,
                next.XPrevL,
                next.Y);
            if (leftDistance > MergeCollinearScreenDistance)
                return false;

            var rightDistance = DistancePointToLine(
                curRightX,
                cur.Y,
                prev.XNextR,
                prev.Y,
                next.XPrevR,
                next.Y);
            return rightDistance <= MergeCollinearScreenDistance;
        }

        /// <summary>
        /// 判断同一采样截面的 Prev/Next 边界是否可视为连续。
        /// </summary>
        private static bool IsBoundaryContinuous(double prevX, double nextX)
            => Math.Abs(nextX - prevX) <= MergeCollinearScreenDistance;

        /// <summary>
        /// 计算点到线段的屏幕距离。
        /// </summary>
        private static double DistancePointToLine(double pointX, double pointY, double lineFromX, double lineFromY, double lineToX, double lineToY)
        {
            var lineX = lineToX - lineFromX;
            var lineY = lineToY - lineFromY;
            var lineLengthSquared = lineX * lineX + lineY * lineY;
            if (lineLengthSquared <= double.Epsilon)
            {
                var offsetX = pointX - lineFromX;
                var offsetY = pointY - lineFromY;
                return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            }

            var projectedRate = ((pointX - lineFromX) * lineX + (pointY - lineFromY) * lineY) / lineLengthSquared;
            if (projectedRate <= 0)
            {
                var offsetX = pointX - lineFromX;
                var offsetY = pointY - lineFromY;
                return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            }

            if (projectedRate >= 1)
            {
                var offsetX = pointX - lineToX;
                var offsetY = pointY - lineToY;
                return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
            }

            var cross = (pointX - lineFromX) * lineY - (pointY - lineFromY) * lineX;
            return Math.Abs(cross) / Math.Sqrt(lineLengthSquared);
        }

        /// <summary>
        /// 把相邻采样截面连接成三角形并提交绘制。
        /// </summary>
        private static void DrawFieldQuads(
            IDrawCommandListBuilder builder,
            IList<PlayFieldLimitParam> limitParams,
            Vector4 playFieldForegroundColor,
            int visibleMinTotalTGrid,
            int visibleMaxTotalTGrid,
            float visibleMinY,
            float visibleMaxY)
        {
            using var vertices = ObjectPool.GetPooledList<PolygonVertex>();
            for (var i = 0; i < limitParams.Count - 1; i++)
            {
                var cur = limitParams[i];
                var next = limitParams[i + 1];
                if (!cur.IsValid || !next.IsValid)
                    continue;

                var clippedFromTotalTGrid = Math.Max(cur.TotalTGrid, visibleMinTotalTGrid);
                var clippedToTotalTGrid = Math.Min(next.TotalTGrid, visibleMaxTotalTGrid);
                var hasVisibleTGridOverlap = clippedToTotalTGrid > clippedFromTotalTGrid;
                var touchesVisibleBoundary = IsRangeBoundarySegment(cur.TotalTGrid, next.TotalTGrid, visibleMinTotalTGrid, visibleMaxTotalTGrid);
                // 可见范围由屏幕 Y 反算 TGrid 时会取整，贴边相邻段仍可能穿过视口。
                if (!hasVisibleTGridOverlap && !touchesVisibleBoundary)
                    continue;

                var fromL = cur.XNextL;
                var fromR = cur.XNextR;
                var fromY = cur.Y;
                var toL = next.XPrevL;
                var toR = next.XPrevR;
                var toY = next.Y;

                if (hasVisibleTGridOverlap && cur.TotalTGrid < clippedFromTotalTGrid)
                {
                    var rate = CalculateTotalTGridRate(cur.TotalTGrid, next.TotalTGrid, clippedFromTotalTGrid);
                    fromL = Lerp(cur.XNextL, next.XPrevL, rate);
                    fromR = Lerp(cur.XNextR, next.XPrevR, rate);
                    fromY = Lerp(cur.Y, next.Y, rate);
                }

                if (hasVisibleTGridOverlap && clippedToTotalTGrid < next.TotalTGrid)
                {
                    var rate = CalculateTotalTGridRate(cur.TotalTGrid, next.TotalTGrid, clippedToTotalTGrid);
                    toL = Lerp(cur.XNextL, next.XPrevL, rate);
                    toR = Lerp(cur.XNextR, next.XPrevR, rate);
                    toY = Lerp(cur.Y, next.Y, rate);
                }

                if (!ClipQuadToVisibleY(
                        ref fromL,
                        ref fromR,
                        ref fromY,
                        ref toL,
                        ref toR,
                        ref toY,
                        visibleMinY,
                        visibleMaxY))
                    continue;

                if (Math.Abs(toY - fromY) < MinQuadScreenHeight)
                    continue;

                if (fromL > fromR || toL > toR)
                    continue;

                // 当前截面的 Next 连接到下一个截面的 Prev，组成一个四边形。
                vertices.Add(new(new(fromL, fromY), playFieldForegroundColor));
                vertices.Add(new(new(toL, toY), playFieldForegroundColor));
                vertices.Add(new(new(fromR, fromY), playFieldForegroundColor));

                vertices.Add(new(new(fromR, fromY), playFieldForegroundColor));
                vertices.Add(new(new(toL, toY), playFieldForegroundColor));
                vertices.Add(new(new(toR, toY), playFieldForegroundColor));
            }

            if (vertices.Count > 0)
                builder.DrawPolygon(Primitive.Triangles, vertices);
        }

        /// <summary>
        /// 判断采样段是否正好贴住当前可见 TGrid 范围边界。
        /// </summary>
        private static bool IsRangeBoundarySegment(int fromTotalTGrid, int toTotalTGrid, int visibleMinTotalTGrid, int visibleMaxTotalTGrid)
            => (fromTotalTGrid <= visibleMinTotalTGrid && visibleMinTotalTGrid <= toTotalTGrid)
               || (fromTotalTGrid <= visibleMaxTotalTGrid && visibleMaxTotalTGrid <= toTotalTGrid);

        /// <summary>
        /// 计算一个 TGrid 在相邻采样截面之间的插值比例。
        /// </summary>
        private static float CalculateTotalTGridRate(int fromTotalTGrid, int toTotalTGrid, int targetTotalTGrid)
        {
            var span = toTotalTGrid - fromTotalTGrid;
            if (span == 0)
                return 0;

            return (float)((targetTotalTGrid - fromTotalTGrid) / (double)span);
        }

        /// <summary>
        /// 把一个四边形沿屏幕 Y 裁剪到当前视口内。
        /// </summary>
        private static bool ClipQuadToVisibleY(
            ref float fromL,
            ref float fromR,
            ref float fromY,
            ref float toL,
            ref float toR,
            ref float toY,
            float visibleMinY,
            float visibleMaxY)
        {
            if (visibleMaxY < visibleMinY)
                (visibleMinY, visibleMaxY) = (visibleMaxY, visibleMinY);

            var segmentMinY = Math.Min(fromY, toY);
            var segmentMaxY = Math.Max(fromY, toY);
            if (segmentMaxY < visibleMinY || visibleMaxY < segmentMinY)
                return false;

            var ySpan = toY - fromY;
            if (Math.Abs(ySpan) <= float.Epsilon)
                return visibleMinY <= fromY && fromY <= visibleMaxY;

            var fromRate = 0f;
            var toRate = 1f;
            if (ySpan > 0)
            {
                if (fromY < visibleMinY)
                    fromRate = CalculateYRate(fromY, toY, visibleMinY);
                if (toY > visibleMaxY)
                    toRate = CalculateYRate(fromY, toY, visibleMaxY);
            }
            else
            {
                if (fromY > visibleMaxY)
                    fromRate = CalculateYRate(fromY, toY, visibleMaxY);
                if (toY < visibleMinY)
                    toRate = CalculateYRate(fromY, toY, visibleMinY);
            }

            if (toRate < fromRate)
                return false;

            var clippedFromL = Lerp(fromL, toL, fromRate);
            var clippedFromR = Lerp(fromR, toR, fromRate);
            var clippedFromY = Lerp(fromY, toY, fromRate);
            var clippedToL = Lerp(fromL, toL, toRate);
            var clippedToR = Lerp(fromR, toR, toRate);
            var clippedToY = Lerp(fromY, toY, toRate);

            fromL = clippedFromL;
            fromR = clippedFromR;
            fromY = clippedFromY;
            toL = clippedToL;
            toR = clippedToR;
            toY = clippedToY;

            return true;
        }

        /// <summary>
        /// 计算一个 Y 坐标在相邻采样截面之间的插值比例。
        /// </summary>
        private static float CalculateYRate(float fromY, float toY, float targetY)
            => (targetY - fromY) / (toY - fromY);

        /// <summary>
        /// 线性插值两个屏幕坐标值。
        /// </summary>
        private static float Lerp(float from, float to, float rate)
            => from + (to - from) * rate;

        /// <summary>
        /// 判断数值是否可用于绘制计算。
        /// </summary>
        private static bool IsValueValid(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// 调试模式下绘制采样点位置。
        /// </summary>
        [System.Diagnostics.Conditional("PLAYFIELD_DEBUG")]
        private static void DebugDrawSamples(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IList<PlayFieldLimitParam> limitParams)
        {
            var validColor = new Vector4(1, 1, 0, 0.75f);
            var invalidColor = new Vector4(1, 0, 0, 0.75f);
            builder.DrawCircles(limitParams.SelectMany(x => new[]
            {
                new CircleInstance(new(x.XNextL, x.Y), x.IsValid ? validColor : invalidColor, false, 6, 0),
                new CircleInstance(new(x.XNextR, x.Y), x.IsValid ? validColor : invalidColor, false, 6, 0),
            }));
        }
    }
}
