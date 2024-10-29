using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;
using static OngekiFumenEditor.Utils.MathUtils;
using System.Linq;
using OngekiFumenEditor.Utils;
using System.Numerics;
using OngekiFumenEditor.Utils.ObjectPool;
using EarcutNet;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
    public class DrawPlayableAreaHelper
    {
        static readonly Vector4 debugLeftColor = new Vector4(1, 51f / 255, 51f / 255, 0.75f);
        static readonly Vector4 debugRightColor = new Vector4(0, 204f / 255, 102f / 255, 0.75f);

        private ILineDrawing lineDrawing;
        private IPolygonDrawing polygonDrawing;
        private ICircleDrawing circleDrawing;
        private IStringDrawing stringDrawing;

        private Vector4 playFieldForegroundColor;
        private bool enablePlayFieldDrawing;

        LineVertex[] vertices = new LineVertex[2];

        public DrawPlayableAreaHelper()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
            polygonDrawing = IoC.Get<IPolygonDrawing>();
            circleDrawing = IoC.Get<ICircleDrawing>();
            stringDrawing = IoC.Get<IStringDrawing>();

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

        public void Draw(IFumenEditorDrawingContext target)
        {
            if (target.Editor.IsDesignMode)
                DrawAudioDuration(target);
        }

        private void DrawAudioDuration(IFumenEditorDrawingContext target)
        {
            var y = (float)target.Editor.TotalDurationHeight;

            var color = new Vector4(1, 0, 0, 1);
            vertices[0] = new(new(0, y), color, VertexDash.Solider);
            vertices[1] = new(new(target.ViewWidth, y), color, VertexDash.Solider);

            lineDrawing.Draw(target, vertices, 3);
        }

        public void DrawPlayField(IFumenEditorDrawingContext target, TGrid minTGrid, TGrid maxTGrid)
        {
            if (target.Editor.IsDesignMode || !enablePlayFieldDrawing)
                return;

            var fumen = target.Editor.Fumen;
            var soflanList = fumen.Soflans.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList);

            var minIdx = soflanList.LastOrDefaultIndexByBinarySearch(minTGrid, x => x.TGrid);
            var maxIdx = soflanList.LastOrDefaultIndexByBinarySearch(maxTGrid, x => x.TGrid);

            // ---|------o----|-----------------------------|---o------|---
            //           x    x                             x   x

            var curSoflanPoint = soflanList[minIdx];
            var rangeInfos = ObjectPool<List<(TGrid tGrid, double speed)>>.Get();
            rangeInfos.Clear();
            rangeInfos.Add((minTGrid, soflanList[minIdx].Speed));

            for (int i = minIdx + 1; i <= maxIdx; i++)
            {
                var soflanPoint = soflanList[i];

                if (soflanPoint.Speed * rangeInfos[^1].speed < 0)
                    rangeInfos.Add((soflanPoint.TGrid, soflanPoint.Speed));

                curSoflanPoint = soflanPoint;
            }

            if (rangeInfos[^1].tGrid != maxTGrid)
                rangeInfos.Add((maxTGrid, rangeInfos[^1].speed));

            for (int i = 0; i < rangeInfos.Count - 1; i++)
            {
                var segMinTGrid = rangeInfos[i].tGrid;
                var segMaxTGrid = rangeInfos[i + 1].tGrid;

                var isPlayback = rangeInfos[i].speed < 0;

                DrawPlayFieldInternal(target, segMinTGrid, segMaxTGrid, isPlayback);
            }

            ObjectPool<List<(TGrid, double)>>.Return(rangeInfos);
        }

        public void DrawPlayFieldInternal(IFumenEditorDrawingContext target, TGrid minTGrid, TGrid maxTGrid, bool isPlaybackSoflan)
        {
            /*
			 画游戏(黑色可移动)区域
				1. 计算一组轨道，每个轨道的节点都算一个point，如果存在轨道相交，那么相交点也算point
				   如果一个水平面(即y相同)存在多个轨道头尾节点，那么就会分别算point
				2. 排列 point集合, 然后简化point和补全point
				3. 将 points集合两两成线，得到线的range[minY, maxY] , 得到Y对应的轨道以及在范围range内轨道所有节点
				4. 将左右所有的节点合并成一个多边形，渲染
			 */

            const long defaultLeftX = -24 * XGrid.DEFAULT_RES_X;
            const long defaultRightX = 24 * XGrid.DEFAULT_RES_X;

            var fumen = target.Editor.Fumen;
            var currentTGrid = target.Editor.GetCurrentTGrid();

            void EnumeratePoints(bool isRight, List<Vector2> result)
            {
                var defaultX = isRight ? defaultRightX : defaultLeftX;
                var type = isRight ? LaneType.WallRight : LaneType.WallLeft;
                var ranges = CombinableRange<int>.CombineRanges(fumen.Lanes.GetVisibleStartObjects(minTGrid, maxTGrid)
                    .Where(x => x.LaneType == type)
                    .Select(x => new CombinableRange<int>(x.MinTGrid.TotalGrid, x.MaxTGrid.TotalGrid)))
                    .OrderBy(x => isRight ? x.Max : x.Min).ToArray();

                var points = ObjectPool<HashSet<float>>.Get();
                points.Clear();

                var prevX = (float)Random();
                var prevY = (float)Random();

                void appendPoint2(List<Vector2> list, float totalXGrid, float totalTGrid)
                {
                    var px = (float)XGridCalculator.ConvertXGridToX(totalXGrid / XGrid.DEFAULT_RES_X, target.Editor);
                    var py = (float)target.ConvertToY(totalTGrid / TGrid.DEFAULT_RES_T);

                    appendPoint3(list, px, py, list.Count);
                }

                void appendPoint3(List<Vector2> list, float px, float py, int insertIdx)
                {
                    var po = list.ElementAtOrDefault(insertIdx);
                    if (po.X == px && po.Y == py)
                        return;

                    var p = new Vector2(px, py);
                    list.Insert(insertIdx, p);

                    //DebugPrintPoint(p, isRight, false, 10);
                }

                void appendPoint(List<Vector2> list, XGrid xGrid, float y)
                {
                    if (xGrid is null)
                        return;
                    appendPoint2(list, xGrid.TotalGrid, y);
                }

                for (int i = 0; i < ranges.Length; i++)
                {
                    var curRange = ranges[i];
                    var nextRange = ranges.ElementAtOrDefault(i + 1);

                    var lanes = fumen.Lanes
                        .GetVisibleStartObjects(TGrid.FromTotalGrid(curRange.Min), TGrid.FromTotalGrid(curRange.Max))
                        .Where(x => x.LaneType == type)
                        .ToArray();

                    var polylines = lanes
                        .SelectMany(x =>
                            x.GenAllPath()
                            .Where(x => minTGrid.TotalGrid <= x.pos.Y && x.pos.Y <= maxTGrid.TotalGrid)
                            .Select(x => x.pos)
                            .SequenceConsecutivelyWrap(2)
                            .Select(x => (x.FirstOrDefault(), x.LastOrDefault())))
                        .ToList();

                    polylines.SortBy(x => x.Item1.Y);

                    for (int r = 0; r < polylines.Count; r++)
                    {
                        var a = polylines[r];
                        for (int t = r + 1; t < polylines.Count; t++)
                        {
                            var b = polylines[t];

                            if (a == b)
                                continue;

                            if (a.Item2.Y < b.Item1.Y)
                                break;

                            if (GetLinesIntersection(a.Item1.ToSystemNumericsVector2(), a.Item2.ToSystemNumericsVector2(), b.Item1.ToSystemNumericsVector2(), b.Item2.ToSystemNumericsVector2()) is Vector2 p)
                                points.Add(p.Y);
                        }
                    }

                    points.AddRange(lanes
                        .Select(x => (float)x.TGrid.TotalGrid)
                        .Concat(lanes.Select(x => x.Children.LastOrDefault())
                        .FilterNull()
                        .Select(x => (float)x.TGrid.TotalGrid))
                        .Where(x => curRange.Min <= x && x <= curRange.Max)
                        );
                }

                var sortedPoints = points.Where(x => minTGrid.TotalGrid < x && x < maxTGrid.TotalGrid).OrderBy(x => x).ToList();

                sortedPoints.InsertBySortBy(minTGrid.TotalGrid, x => x);
                sortedPoints.InsertBySortBy(maxTGrid.TotalGrid, x => x);

                var segments = sortedPoints.SequenceConsecutivelyWrap(2).Select(x => (x.FirstOrDefault(), x.LastOrDefault())).ToArray();

                foreach ((var fromY, var toY) in segments)
                {
                    var midY = (fromY + toY) / 2;
                    var midTGrid = TGrid.FromTotalGrid((int)midY);

                    //获取这个segement范围内要选取的轨道
                    var pickables = fumen.Lanes
                            .GetVisibleStartObjects(midTGrid, midTGrid)
                            .Where(x => x.LaneType == type)
                            .Select(x => (x.CalulateXGrid(midTGrid), x))
                            .FilterNullBy(x => x.Item1)
                            .ToArray();

                    //选取轨道，如果存在多个轨道(即轨道交叉冲突了)，那么就按左右边规则选取一个
                    (_, var pickLane) = pickables.IsEmpty() ? default : (isRight ? pickables.MaxBy(x => x.Item1) : pickables.MinBy(x => x.Item1));
                    if (pickLane is not null)
                    {
                        var fromTGrid = TGrid.FromTotalGrid((int)fromY);
                        appendPoint(result, pickLane.CalulateXGrid(fromTGrid), fromY);

                        var prevTotalGrid = 0f;
                        foreach (var pos in pickLane.GenAllPath().Select(x => x.pos).SkipWhile(x => x.Y < fromY).TakeWhile(x => x.Y <= toY))
                        {
                            appendPoint2(result, pos.X, pos.Y);
                            prevTotalGrid = pos.Y;
                        }

                        var toTGrid = TGrid.FromTotalGrid((int)toY);
                        if (toTGrid.TotalGrid > prevTotalGrid)
                            appendPoint(result, pickLane.CalulateXGrid(toTGrid), toY);
                    }
                    else
                    {
                        //选取不到轨道，表示这个segement是两个轨道之间的空白区域，那么直接填上空白就行
                        appendPoint2(result, defaultX, fromY);
                        appendPoint2(result, defaultX, toY);
                    }
                }

                //解决变速过快导致的精度丢失问题
                Vector2? interpolate(TGrid tGrid, float actualY, out bool isPickLane)
                {
                    isPickLane = false;
                    var pickables = fumen.Lanes
                            .GetVisibleStartObjects(tGrid, tGrid)
                            .Where(x => x.LaneType == type)
                            .Where(x =>
                            {
                                var laneMinY = target.ConvertToY(x.MinTGrid);
                                var laneMaxY = target.ConvertToY(x.MaxTGrid);

                                return laneMinY <= actualY && actualY <= laneMaxY;
                            })
                            .Select(x => (x.CalulateXGrid(tGrid), x))
                            .FilterNullBy(x => x.Item1)
                            .ToArray();

                    (_, var pickLane) = pickables.IsEmpty() ? default : (isRight ? pickables.MaxBy(x => x.Item1) : pickables.MinBy(x => x.Item1));

                    if (pickLane is not null)
                    {
                        var itor = pickLane.GenAllPath().GetEnumerator();
                        var prevOpt = default(OpenTK.Mathematics.Vector2?);

                        while (itor.MoveNext())
                        {
                            var cur = itor.Current.pos;

                            if (cur.Y > tGrid.TotalGrid)
                            {
                                // prev ------------ cur
                                //           ^
                                //         tGrid

                                if (prevOpt is OpenTK.Mathematics.Vector2 prev)
                                {
                                    var curPx = (float)XGridCalculator.ConvertXGridToX(cur.X / XGrid.DEFAULT_RES_X, target.Editor);
                                    var curPy = (float)target.ConvertToY(cur.Y / TGrid.DEFAULT_RES_T);
                                    var prevPx = (float)XGridCalculator.ConvertXGridToX(prev.X / XGrid.DEFAULT_RES_X, target.Editor);
                                    var prevPy = (float)target.ConvertToY(prev.Y / TGrid.DEFAULT_RES_T);

                                    var nowPy = actualY;
                                    var nowPx = (float)MathUtils.CalculateXFromTwoPointFormFormula(nowPy, prevPx, prevPy, curPx, curPy);
                                    isPickLane = true;
                                    return new(nowPx, nowPy);
                                }
                            }

                            prevOpt = cur;
                        }
                    }
                    else
                    {
                        var defaultPx = (float)XGridCalculator.ConvertXGridToX(defaultX / XGrid.DEFAULT_RES_X, target.Editor);
                        return new(defaultPx, actualY);
                    }

                    return default;
                }

                if (minTGrid <= currentTGrid && currentTGrid <= maxTGrid)
                {
                    var maxY = target.ConvertToY(maxTGrid);
                    var actualMaxY = target.Rect.TopLeft.Y;

                    var maxDiff = maxY - actualMaxY;
                    if (Math.Abs(maxDiff) >= 1)
                    {
                        if (interpolate(maxTGrid, (float)Math.Max(actualMaxY, maxY), out var isPickLane) is Vector2 pp)
                        {
                            if (!isPickLane)
                                appendPoint3(result, (float)XGridCalculator.ConvertXGridToX(defaultX / XGrid.DEFAULT_RES_X, target.Editor), result.LastOrDefault().Y, result.Count);
                            appendPoint3(result, pp.X, pp.Y, result.Count);
                        }
                    }

                    var minY = target.ConvertToY(minTGrid);
                    var actualMinY = target.Rect.ButtomRight.Y;

                    var minDiff = minY - actualMinY;
                    if (Math.Abs(minDiff) >= 1)
                    {
                        if (interpolate(minTGrid, (float)Math.Max(actualMinY, minY), out var isPickLane) is Vector2 pp)
                        {
                            if (!isPickLane)
                                appendPoint3(result, (float)XGridCalculator.ConvertXGridToX(defaultX / XGrid.DEFAULT_RES_X, target.Editor), result.FirstOrDefault().Y, 0);
                            appendPoint3(result, pp.X, pp.Y, 0);
                        }
                    }
                }

                //optimze points
                for (var i = 0; i < result.Count; i++)
                {
                    if (i > 0)
                    {
                        if (result[i] == result[i - 1])
                        {
                            //remove dup
                            result.RemoveAt(i - 1);
                            i--;
                        }
                    }

                    if (i > 1)
                    {
                        // prev2 --- prev --- cur
                        var prev2 = result[i - 2];
                        var prev = result[i - 1];
                        var cur = result[i];

                        if ((prev.Y == cur.Y && prev.Y == prev2.Y) || (prev.X == cur.X && prev.X == prev2.X))
                        {
                            //optimze prev point if able
                            result.RemoveAt(i - 1);
                            i--;
                        }
                    }
                }

                ObjectPool<HashSet<float>>.Return(points);
            }

            using var d3 = ObjectPool<List<double>>.GetWithUsingDisposable(out var tessellatePoints, out _);
            tessellatePoints.Clear();
            using var d4 = ObjectPool<List<int>>.GetWithUsingDisposable(out var idxList, out _);
            idxList.Clear();

            using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var leftPoints, out _);
            leftPoints.Clear();
            using var d2 = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var rightPoints, out _);
            rightPoints.Clear();

            //计算左右墙的点
            EnumeratePoints(false, leftPoints);
            EnumeratePoints(true, rightPoints);

            //解决左右墙交叉处理问题
            AdjustLaneIntersection(target, leftPoints, rightPoints);

            //合并提交，准备进行三角剖分
            foreach (var pos in leftPoints)
            {
                tessellatePoints.Add(pos.X);
                tessellatePoints.Add(pos.Y);
            }
            foreach (var pos in rightPoints.AsEnumerable().Reverse())
            {
                tessellatePoints.Add(pos.X);
                tessellatePoints.Add(pos.Y);
            }

            var tessellateList = ObjectPool<List<int>>.Get();
            tessellateList.Clear();
            Earcut.Tessellate(tessellatePoints, idxList, tessellateList);

            polygonDrawing.Begin(target, OpenTK.Graphics.OpenGL.PrimitiveType.Triangles);
            foreach (var seq in tessellateList.SequenceWrap(3))
            {
                foreach (var idx in seq)
                {
                    var x = (float)tessellatePoints[idx * 2 + 0];
                    var y = (float)tessellatePoints[idx * 2 + 1];
                    polygonDrawing.PostPoint(new(x, y), playFieldForegroundColor);
                }
            }
            polygonDrawing.End();


#if PLAYFIELD_DEBUG
            playFieldForegroundColor.W = 0.4f;
            lineDrawing.Draw(target, leftPoints.Select(p => new LineVertex(p, debugLeftColor, VertexDash.Solider)), 6);
            lineDrawing.Draw(target, rightPoints.Select(p => new LineVertex(p, debugRightColor, VertexDash.Solider)), 6);

            var i = 0;
            foreach (var seq in tessellateList.SequenceWrap(3))
            {
                var (r, g, b) = Hsl2Rgb(39, 1f, 0.5f);
                var color = new Vector4(r, g, b, 0.9f);

                lineDrawing.Draw(target, seq.Append(seq.FirstOrDefault())
                    .Select(idx => new LineVertex(new((float)tessellatePoints[idx * 2 + 0], (float)tessellatePoints[idx * 2 + 1]), color, new(6, 4))), 2);

                i += 3;
            }

            void printPoints(IEnumerable<Vector2> data, Vector4 color, bool isRight)
            {
                color.W = 1;
                circleDrawing.Begin(target);
                foreach (var pos in data)
                    circleDrawing.Post(pos, color, false, 10);
                circleDrawing.End();
                var prevY = 0f;
                var prevR = 0;
                foreach (var pos in data)
                {
                    if (prevY == pos.Y)
                        prevR = prevR switch { 1 => 0, 0 => 1 };
                    else
                        prevR = 0;
                    prevY = pos.Y;

                    stringDrawing.Draw(
                        $"({pos.X}, {pos.Y})",
                        pos - new Vector2(isRight ? -10 : 10, -prevR * 10),
                        Vector2.One,
                        15,
                        0,
                        color,
                        new(isRight ? 0 : 1, prevR),
                        default,
                        target,
                        default,
                        out _
                        );
                }
            }

            printPoints(leftPoints, debugLeftColor, false);
            printPoints(rightPoints, debugRightColor, true);
#endif

            ObjectPool<List<int>>.Return(tessellateList);
        }

        private void AdjustLaneIntersection(IDrawingContext target, List<Vector2> leftPoints, List<Vector2> rightPoints)
        {
            using var d = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var tempLeft, out _);
            using var d2 = ObjectPool<List<Vector2>>.GetWithUsingDisposable(out var tempRight, out _);
            using var d3 = ObjectPool<HashSet<Vector2>>.GetWithUsingDisposable(out var intersectionPoints, out _);
            intersectionPoints.Clear();

            tryExchange(0, 0);

            var leftIdx = 1;
            var rightIdx = 1;

            bool tryExchange(int li, int ri)
            {
                tempLeft.Clear();
                tempRight.Clear();

                var lpIdx = li;
                var rpIdx = ri;
                var lp = leftPoints[lpIdx];
                var rp = rightPoints[rpIdx];

                var ck = true;
                while (lp == rp && ck)
                {
                    ck = false;
                    if (lpIdx + 1 < leftPoints.Count)
                    {
                        lpIdx++;
                        lp = leftPoints[lpIdx];
                        ck = true;
                    }
                    if (rpIdx + 1 < rightPoints.Count)
                    {
                        rpIdx++;
                        rp = rightPoints[rpIdx];
                        ck = true;
                    }
                }

                if (lp.X < rp.X)
                    return false;

                tempLeft.AddRange(leftPoints[li..]);
                tempRight.AddRange(rightPoints[ri..]);

                leftPoints.RemoveRange(li, tempLeft.Count);
                rightPoints.RemoveRange(ri, tempRight.Count);

                leftPoints.AddRange(tempRight);
                rightPoints.AddRange(tempLeft);

                return true;
            }

            while (leftIdx < leftPoints.Count && rightIdx < rightPoints.Count)
            {
                (Vector2 from, Vector2 to) leftLine = (leftPoints[leftIdx - 1], leftPoints[leftIdx]);
                (Vector2 from, Vector2 to) rightLine = (rightPoints[rightIdx - 1], rightPoints[rightIdx]);

                if (leftLine == rightLine)
                {
                    leftIdx++;
                    rightIdx++;
                    continue;
                }

                if (GetLinesIntersection(leftLine.from, leftLine.to, rightLine.from, rightLine.to) is Vector2 intersectionPoint && !intersectionPoints.Contains(intersectionPoint))
                {
                    intersectionPoints.Add(intersectionPoint);

                    var isCross = !(intersectionPoint == leftLine.from ||
                        intersectionPoint == leftLine.to ||
                        intersectionPoint == rightLine.from ||
                        intersectionPoint == rightLine.to);


                    if (rightLine.to == intersectionPoint && intersectionPoint == leftLine.to)
                        isCross = true;

                    if (!(rightLine.from.Y == rightLine.to.Y || leftLine.from.Y == leftLine.to.Y))
                        isCross &= true;

                    if (isCross)
                    {
                        if (tryExchange(leftIdx, rightIdx))
                        {
                            leftPoints.Insert(leftIdx, intersectionPoint);
                            rightPoints.Insert(rightIdx, intersectionPoint);
                        }
                    }
                    else
                    {
                        var isRightIntersected = intersectionPoint == rightLine.from ||
                        intersectionPoint == rightLine.to;
                        var isLeftIntersected = intersectionPoint == leftLine.from ||
                        intersectionPoint == leftLine.to;
                        var isFromIntersected = intersectionPoint == (isRightIntersected ? rightLine : leftLine).from;

                        var insertLeftIdx = leftIdx;
                        var insertRightIdx = rightIdx;

                        //将突出的位置拉回来
                        if (isRightIntersected != isLeftIntersected)
                        {
                            //先补个点
                            if (isRightIntersected)
                            {
                                leftPoints.Insert(leftIdx, intersectionPoint);
                                leftIdx++;
                            }
                            else
                            {
                                rightPoints.Insert(rightIdx, intersectionPoint);
                                rightIdx++;
                            }

                            if (isFromIntersected)
                            {
                                if (isRightIntersected)
                                {
                                    rightIdx--;
                                }
                                else
                                {
                                    leftIdx--;
                                }
                            }
                            else
                            {
                                if (isRightIntersected)
                                {
                                    leftIdx--;
                                }
                                else
                                {
                                    rightIdx--;
                                }
                            }

                            //获取左右两点之间的中点
                            var centerX = (leftPoints[leftIdx].X + rightPoints[rightIdx].X) / 2;
                            var centerPoint = new Vector2(centerX, intersectionPoint.Y);

                            //两边再插入中点
                            if (leftPoints[leftIdx] != centerPoint)
                                leftPoints.Insert(leftIdx, centerPoint);
                            if (rightPoints[rightIdx] != centerPoint)
                                rightPoints.Insert(rightIdx, centerPoint);
                        }
                        else
                        {
                            //说明是V字形或者A字形
                            var isVType = rightLine.to.Y > intersectionPoint.Y || leftLine.to.Y > intersectionPoint.Y;
                            var isAType = rightLine.from.Y < intersectionPoint.Y || leftLine.from.Y < intersectionPoint.Y;

                            //重新定位
                            if (isVType)
                            {
                                if (leftLine.to.Y > leftLine.from.Y)
                                {
                                    if (intersectionPoint == leftLine.from)
                                    {
                                        leftIdx--;
                                    }
                                }
                                if (rightLine.to.Y > rightLine.from.Y)
                                {
                                    if (intersectionPoint == rightLine.from)
                                    {
                                        rightIdx--;
                                    }
                                }
                            }
                            else if (isAType)
                            {
                                if (leftLine.to.Y > leftLine.from.Y)
                                {
                                    if (intersectionPoint == leftLine.to)
                                    {
                                        leftIdx--;
                                    }
                                }
                                if (rightLine.to.Y > rightLine.from.Y)
                                {
                                    if (intersectionPoint == rightLine.to)
                                    {
                                        rightIdx--;
                                    }
                                }
                            }
                        }

                        tryExchange(leftIdx, rightIdx);
                    }
#if PLAYFIELD_DEBUG
                    circleDrawing.Begin(target);
                    circleDrawing.Post(intersectionPoint, isCross ? new(1, 1, 0, 0.75f) : new(0, 153 / 255f, 153 / 255f, 0.75f), false, 30);
                    circleDrawing.End();
                    stringDrawing.Draw(
                        $"[{leftIdx}, {rightIdx}]",
                        intersectionPoint - new Vector2(intersectionPoint.X <= target.Rect.CenterX ? -10 : 10, 10),
                        Vector2.One,
                        15,
                        0,
                        new(1, 1, 0, 1),
                        new(intersectionPoint.X <= target.Rect.CenterX ? 0 : 1, 1),
                        default,
                        target,
                        default,
                        out _
                        );
#endif
                    leftIdx++;
                    rightIdx++;
                    continue;
                }

                //看看哪一边idx需要递增
                if (rightLine.from.Y <= leftLine.to.Y && leftLine.to.Y <= rightLine.to.Y)
                {
                    //left的末端在right范围内，那么left需要递增
                    leftIdx++;
                }
                else if (leftLine.from.Y <= rightLine.to.Y && rightLine.to.Y <= leftLine.to.Y)
                {
                    rightIdx++;
                }
                else
                {
                    leftIdx++;
                    rightIdx++;
                }
            }
        }

        public static (float r, float g, float b) Hsl2Rgb(float h, float s, float l)
        {
            // Ensure the Hue is in the range of 0 to 360
            h = h % 360;

            float c = (1 - Math.Abs(2 * l - 1)) * s;  // Chroma
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            float m = l - c / 2;

            float rPrime = 0, gPrime = 0, bPrime = 0;

            if (h >= 0 && h < 60)
            {
                rPrime = c;
                gPrime = x;
            }
            else if (h >= 60 && h < 120)
            {
                rPrime = x;
                gPrime = c;
            }
            else if (h >= 120 && h < 180)
            {
                gPrime = c;
                bPrime = x;
            }
            else if (h >= 180 && h < 240)
            {
                gPrime = x;
                bPrime = c;
            }
            else if (h >= 240 && h < 300)
            {
                rPrime = x;
                bPrime = c;
            }
            else if (h >= 300 && h < 360)
            {
                rPrime = c;
                bPrime = x;
            }

            // Convert to final RGB values by adding m and scaling to the 0-1 range
            float r = rPrime + m;
            float g = gPrime + m;
            float b = bPrime + m;

            return (r, g, b);
        }
    }
}
