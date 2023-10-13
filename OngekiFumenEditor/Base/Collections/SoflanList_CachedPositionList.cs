using IntervalTree;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;

namespace OngekiFumenEditor.Base.Collections
{
    public partial class SoflanList
    {
        public struct SoflanPoint
        {
            public SoflanPoint(double y, TGrid tGrid, double speed, BPMChange bpm)
            {
                Y = y;
                TGrid = tGrid;
                Speed = speed;
                Bpm = bpm;
            }

            public double Y { get; set; }
            public TGrid TGrid { get; set; }
            public double Speed { get; set; }
            public BPMChange Bpm { get; set; }

            public override string ToString() => $"Y:{Y} TGrid:{TGrid} SPD:{Speed} BPM:{Bpm.BPM}";
        }

        #region SoflanPositionList

        private double cachedSoflanListCacheHash = int.MinValue;

        private List<SoflanPoint> cachedSoflanPositionList_DesignMode = new();
        private List<SoflanPoint> cachedSoflanPositionList_PreviewMode = new();

        public record VisibleTGridRange(TGrid minTGrid, TGrid maxTGrid);
        private record VisibleRange(double minY, TGrid minTGrid, double maxY, TGrid maxTGrid);

        private IIntervalTree<double, VisibleRange> cachePostionList_DesignMode;
        private double maxEndY;
        private IIntervalTree<double, VisibleRange> cachePostionList_PreviewMode;

        [Flags]
        public enum ChgEvt
        {
            None = 0,
            BpmChanged = 1,
            SoflanBegan = 2,
            SoflanEnded = 4,
            SoflanChanged = SoflanBegan | SoflanEnded
        }

        public IEnumerable<(TGrid TGrid, double speed, BPMChange curBpm, ChgEvt)> GetCalculatableEvents(BpmList bpmList, bool isDesignModel)
        {
            var curBpm = bpmList.FirstBpm;
            Soflan curSpeedEvent = null;

            IEnumerable<(TGrid TGrid, double speed, BPMChange curBpm, ChgEvt evt)> GetEventTimings(ITimelineObject evt)
            {
                var t = evt.TGrid;
                switch (evt)
                {
                    case BPMChange bpmEvt:
                        curBpm = bpmEvt;
                        var speed = (curSpeedEvent is not null && curSpeedEvent.EndTGrid > t) ? (isDesignModel ? curSpeedEvent.SpeedInEditor : curSpeedEvent.Speed) : 1.0d;
                        yield return (evt.TGrid, speed, curBpm, ChgEvt.BpmChanged);
                        break;
                    case Soflan soflanEvt:
                        curSpeedEvent = soflanEvt;
                        yield return (evt.TGrid, (isDesignModel ? soflanEvt.SpeedInEditor : soflanEvt.Speed), curBpm, ChgEvt.SoflanBegan);
                        var endTGrid = evt.TGrid + new GridOffset(0, soflanEvt.GridLength);
                        yield return (endTGrid, 1.0f, bpmList.GetBpm(endTGrid), ChgEvt.SoflanEnded);
                        break;
                }
            }
            var r = this.AsEnumerable<ITimelineObject>().Concat(bpmList)
                .OrderBy(x => x.TGrid)
                .SelectMany(GetEventTimings)
                .GroupBy(x => x.TGrid)
                .OrderBy(x => x.Key);

            var s = r
                .Select(x =>
                {
                    var itor = x.GetEnumerator();
                    if (itor.MoveNext())
                    {
                        var totalState = itor.Current;
                        while (itor.MoveNext())
                        {
                            var curState = itor.Current;

                            totalState.evt |= curState.evt;
                            switch (curState.evt)
                            {
                                case ChgEvt.SoflanEnded:
                                    if (!totalState.evt.HasFlag(ChgEvt.SoflanBegan))
                                        totalState.speed = curState.speed;
                                    break;
                                case ChgEvt.SoflanBegan:
                                    totalState.speed = curState.speed;
                                    break;
                                case ChgEvt.BpmChanged:
                                    totalState.curBpm = curState.curBpm;
                                    break;
                                default:
                                    break;
                            }
                        }

                        return totalState;
                    }
                    return default;
                })
                .Where(x => x.evt != ChgEvt.None);

            return s;
        }

        private void UpdateCachedSoflanPositionList(double tUnitLength, BpmList bpmList, List<SoflanPoint> list, bool isDesignMode)
        {
            list.Clear();

            var eventList = GetCalculatableEvents(bpmList, isDesignMode);

            var itor = eventList.GetEnumerator();
            if (!itor.MoveNext())
                return; //不应该出现这种情况的
            var currentY = 0d;

            var prevEvent = itor.Current;

            while (itor.MoveNext())
            {
                /* |---------------------------|
                  prev                        cur
                 */
                var curEvent = itor.Current;

                var len = MathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.curBpm.BPM, tUnitLength);

                //todo 什么时候能够实现倒车Soflan显示在处理吧~
                var scaledLen = len * (isDesignMode ? Math.Abs(prevEvent.speed) : prevEvent.speed);
                //var scaledLen = len * Math.Abs(prevEvent.speed);

                var fromY = currentY;
                var toY = currentY + scaledLen;

                list.Add(new(fromY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));

                currentY = toY;
                prevEvent = curEvent;
            }

            if (list.Count == 0)
                list.Add(new(0, TGrid.Zero, 1.0d, bpmList.FirstBpm));
            else if (prevEvent.TGrid != list.First().TGrid)
                list.Add(new(currentY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));
        }

        private IIntervalTree<double, VisibleRange> RebuildIntervalTreePositionList(List<SoflanPoint> list)
        {
            var tree = new IntervalTree<double, VisibleRange>();
            maxEndY = int.MinValue;

            foreach (var pair in list.SequenceConsecutivelyWrap(2).Where(x => !x.IsOnlyOne()))
            {
                var prev = pair.First();
                var next = pair.Last();

                var beginY = Math.Min(prev.Y, next.Y);
                var endY = Math.Max(prev.Y, next.Y);
                maxEndY = Math.Max(maxEndY, endY);

                var beginTGrid = MathUtils.Min(prev.TGrid, next.TGrid);
                var endTGrid = MathUtils.Max(prev.TGrid, next.TGrid);

                tree.Add(beginY, endY, new(beginY, beginTGrid, endY, endTGrid));
            }

            return tree;
        }

        private void CheckAndUpdateSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            var hash = HashCode.Combine(tUnitLength, bpmList.cachedBpmContentHash);

            if (cachedSoflanListCacheHash != hash)
            {
                cachedSoflanListCacheHash = hash;

                Log.LogDebug("recalculate all.");
                UpdateCachedSoflanPositionList(tUnitLength, bpmList, cachedSoflanPositionList_DesignMode, true);
                UpdateCachedSoflanPositionList(tUnitLength, bpmList, cachedSoflanPositionList_PreviewMode, false);
                cachePostionList_PreviewMode = RebuildIntervalTreePositionList(cachedSoflanPositionList_PreviewMode);
            }
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_DesignMode(double tUnitLength, BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);
            return cachedSoflanPositionList_DesignMode;
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_PreviewMode(double tUnitLength, BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);
            return cachedSoflanPositionList_PreviewMode;
        }

        public IEnumerable<VisibleTGridRange> GetVisibleRanges_PreviewMode(double nonScaleMinY, double nonScaleMaxY, BpmList bpmList, double tUnitLength)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);

            VisibleTGridRange TrimedTGridRange(VisibleRange visibleRange)
            {
                var trimedMinTGrid = visibleRange.minTGrid;
                var trimedMaxTGrid = visibleRange.maxTGrid;

                var yLen = visibleRange.maxY - visibleRange.minY;
                var gridLen = (visibleRange.maxTGrid - visibleRange.minTGrid).TotalGrid(visibleRange.minTGrid.GridRadix);

                if (visibleRange.minY < nonScaleMinY)
                {
                    var diff = nonScaleMinY - visibleRange.minY;
                    var p = diff / yLen;
                    trimedMinTGrid = trimedMinTGrid + new GridOffset(0, (int)(p * gridLen));
                }

                if (nonScaleMaxY < visibleRange.maxY)
                {
                    var diff = visibleRange.maxY - nonScaleMaxY;
                    var p = diff / yLen;
                    trimedMaxTGrid = trimedMaxTGrid - new GridOffset(0, (int)(p * gridLen));
                }

                return new(trimedMinTGrid, trimedMaxTGrid);
            }

            var genDefault = false;
            if (cachePostionList_PreviewMode.Count > 0)
            {
                var result = cachePostionList_PreviewMode.Query(nonScaleMinY, nonScaleMaxY)
                    .Select(TrimedTGridRange)
                    .OrderBy(x => x.minTGrid);
                var itor = result.GetEnumerator();
                if (!itor.MoveNext())
                {
                    if (nonScaleMinY > maxEndY && nonScaleMaxY > maxEndY)
                        genDefault = true;
                    else
                        yield break;
                }
                var cur = itor.Current;
                while (itor.MoveNext())
                {
                    var next = itor.Current;
                    if (next.minTGrid <= cur.maxTGrid)
                    {
                        //combinable
                        cur = new(MathUtils.Min(cur.minTGrid, next.minTGrid), MathUtils.Max(cur.maxTGrid, next.maxTGrid));
                    }
                    else
                    {
                        yield return cur;
                        cur = next;
                    }
                }
                if (cur is not null)
                    yield return cur;
            }
            else
                genDefault = true;

            if (genDefault)
            {
                var pos = GetCachedSoflanPositionList_PreviewMode(tUnitLength, bpmList).Last();

                var gridOffset = pos.Bpm.LengthConvertToOffset(nonScaleMinY - pos.Y, (int)tUnitLength);
                var minTGrid = pos.TGrid + gridOffset;

                gridOffset = pos.Bpm.LengthConvertToOffset(nonScaleMaxY - pos.Y, (int)tUnitLength);
                var maxTGrid = pos.TGrid + gridOffset;

                var range = new VisibleTGridRange(MathUtils.Min(minTGrid, maxTGrid), MathUtils.Max(minTGrid, maxTGrid));

                yield return range;
            }
        }

        public IEnumerable<VisibleTGridRange> _GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale, int tUnitLength)
        {
            var actualViewHeight = viewHeight / scale;
            var actualViewMinY = currentY - preOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            var list = GetCachedSoflanPositionList_PreviewMode(tUnitLength, bpmList);

            IEnumerable<VisibleTGridRange> TryMerge(IEnumerable<VisibleTGridRange> sortedList)
            {
                var itor = sortedList.OrderBy(x => x.minTGrid).GetEnumerator();
                if (!itor.MoveNext())
                    yield break;
                var cur = itor.Current;
                while (itor.MoveNext())
                {
                    var next = itor.Current;
                    if (next.minTGrid <= cur.maxTGrid)
                    {
                        //combinable
                        cur = new(MathUtils.Min(cur.minTGrid, next.minTGrid), MathUtils.Max(cur.maxTGrid, next.maxTGrid));
                    }
                    else
                    {
                        yield return cur;
                        cur = next;
                    }
                }
                if (cur is not null)
                    yield return cur;
            }

            IEnumerable<VisibleTGridRange> CalcSegment(int posIdx, double y, double leftRemain, double rightRemain)
            {
                var cur = list[posIdx];
                var next = list[posIdx + 1];

                var leftMergeds = Enumerable.Empty<VisibleTGridRange>();
                var curTGrid = default(VisibleTGridRange);
                var leftTGrid = default(TGrid);
                var rightTGrid = default(TGrid);
                var rightMergeds = Enumerable.Empty<VisibleTGridRange>();

                var left = 0d;
                var right = 0d;
                var newLeftRemain = 0d;
                var newRightRemain = 0d;

                var absSpeed = Math.Abs(cur.Speed);

                if (cur.Speed > 0)
                {
                    var calcLeftY = y - leftRemain;
                    left = Math.Max(calcLeftY, cur.Y);
                    newLeftRemain = Math.Max(cur.Y - calcLeftY, 0);
                    leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed, tUnitLength));

                    var calcRightY = y + rightRemain;
                    right = Math.Min(next.Y, calcRightY);
                    newRightRemain = Math.Max(calcRightY - next.Y, 0);
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed, tUnitLength));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }
                else
                {
                    var calcLeftY = y + leftRemain;
                    left = Math.Min(calcLeftY, cur.Y);
                    newLeftRemain = Math.Max(-cur.Y + left, 0);
                    //问题是倒车时，left实际显示范围比用户指定的leftRemain还要大，因此实际上还得合并整个viewHeight
                    leftTGrid = (cur.TGrid - cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, (cur.Y - left)) / absSpeed, tUnitLength)) ?? TGrid.Zero;
                    
                    var calcRightY = y - rightRemain;
                    right = Math.Max(next.Y, calcRightY);
                    newRightRemain = Math.Max(next.Y - calcRightY, 0);
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed, tUnitLength));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }

                //Log.LogDebug($"{{{cur.TGrid}({cur.Y})  -->  {next.TGrid}({next.Y})}}  calc({leftRemain}|{y}|{rightRemain})  {{{leftTGrid}({left}){newLeftRemain}  -->  {rightTGrid}({right}){newRightRemain}}}");

                if (newLeftRemain > 0)
                {
                    if (posIdx > 0)
                        leftMergeds = CalcSegment(posIdx - 1, left, newLeftRemain, 0);
                    else
                    {
                        var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed, tUnitLength));
                        overLeftTGrid = overLeftTGrid ?? TGrid.Zero;
                        leftMergeds = leftMergeds.Append(new VisibleTGridRange(overLeftTGrid, leftTGrid));
                    }
                }

                if (newRightRemain > 0)
                {
                    if (posIdx < list.Count - 2)
                        rightMergeds = CalcSegment(posIdx + 1, right, 0, newRightRemain);
                    else
                    {
                        var overRightTGrid = rightTGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newRightRemain / absSpeed, tUnitLength));
                        rightMergeds = rightMergeds.Append(new VisibleTGridRange(rightTGrid, overRightTGrid));
                    }
                }

                return leftMergeds.Append(curTGrid).Concat(rightMergeds);
            }

            IEnumerable<VisibleTGridRange> _internal()
            {
                var minY = 0d;
                var maxY = 0d;
                var cur = default(SoflanPoint);

                if (list.Count > 1)
                {
                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        cur = list[i];
                        var next = list[i + 1];

                        minY = Math.Min(cur.Y, next.Y);
                        maxY = Math.Max(cur.Y, next.Y);

                        if ((minY <= currentY && currentY <= maxY) || actualViewMaxY >= minY && maxY >= actualViewMinY)
                        {
                            var mergeds = CalcSegment(i, currentY, preOffset, actualViewHeight - preOffset);
                            foreach (var range in mergeds)
                            {
                                yield return range;
                            }
                        }
                    }

                    cur = list.Last();
                    maxY = currentY + (actualViewHeight - preOffset);
                    minY = currentY - preOffset;
                    var absSpeed = Math.Abs(cur.Speed);

                    if (cur.Y <= minY)
                    {
                        absSpeed = 1;
                        var gridOffset = cur.Bpm.LengthConvertToOffset(minY - cur.Y, (int)tUnitLength);
                        var minTGrid = cur.TGrid + gridOffset;

                        gridOffset = cur.Bpm.LengthConvertToOffset(maxY - cur.Y, (int)tUnitLength);
                        var maxTGrid = cur.TGrid + gridOffset;

                        var range = new VisibleTGridRange(MathUtils.Min(minTGrid, maxTGrid), MathUtils.Max(minTGrid, maxTGrid));
                        yield return range;
                    }
                    else
                    {
                        var leftTGrid = cur.TGrid;

                        var right = cur.Y + actualViewHeight;
                        var rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed, tUnitLength));

                        yield return new(leftTGrid, rightTGrid);
                    }
                }
                else
                {
                    cur = list[0];
                    var absSpeed = Math.Abs(cur.Speed);

                    if (cur.Speed > 0)
                    {
                        minY = currentY - preOffset;
                        maxY = minY + actualViewHeight;

                        var left = Math.Max(0, minY) / scale;
                        var leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(left / absSpeed, tUnitLength));

                        var right = left + actualViewHeight;
                        var rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(right / absSpeed, tUnitLength));

                        yield return new(leftTGrid, rightTGrid);
                    }
                    else
                    {
                        //todo maybe?
                    }
                }
            }

            return TryMerge(_internal());
        }
    }

    #endregion
}
