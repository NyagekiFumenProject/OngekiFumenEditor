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
using System.Windows.Media.Media3D;

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

        public record SoflanSegment(int curIdx, SoflanPoint cur, SoflanPoint next);

        private IIntervalTree<double, SoflanSegment> cachePostionList_PreviewMode;

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

        private IIntervalTree<double, SoflanSegment> RebuildIntervalTreePositionList(List<SoflanPoint> list)
        {
            var tree = new IntervalTree<double, SoflanSegment>();

            for (int i = 0; i < list.Count - 1; i++)
            {
                var prev = list[i];
                var next = list[i + 1];

                var beginY = Math.Min(prev.Y, next.Y);
                var endY = Math.Max(prev.Y, next.Y);

                tree.Add(beginY, endY, new(i, prev, next));
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

        public IIntervalTree<double, SoflanSegment> GetCachedSoflanSegment_PreviewMode(double tUnitLength, BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);
            return cachePostionList_PreviewMode;
        }

        /// <summary>
        /// 通过当前坐标信息，逆推计算出获取可视TGrid范围
        /// (整个项目最恶心但最重要的实现之一)
        /// </summary>
        /// <param name="currentY">当前位置</param>
        /// <param name="viewHeight">可视范围</param>
        /// <param name="preOffset">前视范围(一般指判定线的偏移量)</param>
        /// <param name="bpmList"></param>
        /// <param name="scale"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        public IEnumerable<VisibleTGridRange> GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale, int tUnitLength)
        {
            currentY = currentY / scale;
            var actualViewHeight = viewHeight / scale;
            var actualPreOffset = preOffset / scale;
            var actualViewMinY = currentY - actualPreOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            using var _d1 = ObjectPool<HashSet<int>>.GetWithUsingDisposable(out var fullCheckSets, out _);
            fullCheckSets.Clear();

            var list = GetCachedSoflanPositionList_PreviewMode(tUnitLength, bpmList);
            var segments = GetCachedSoflanSegment_PreviewMode(tUnitLength, bpmList);

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
                if (fullCheckSets.Contains(posIdx))
                    return Enumerable.Empty<VisibleTGridRange>();

                /*
                 LEFT    ------->    RIGHT
             cur(posIdx)           next(posIdx+1)
                  |--------------------|----....--->
                       ↑           ↑
                       |---o-------|
              leftRemain   y       rightRemain

                 posIdx = 变速段位置
                 y = 当前位置
                 leftRemain = 向前探测剩余量
                 rightRemain = 向后探测剩余量
                 */
                var cur = list[posIdx];//当前变速信息
                var next = list[posIdx + 1];
                var absSpeed = Math.Abs(cur.Speed);

                var leftMergeds = Enumerable.Empty<VisibleTGridRange>();
                var curTGrid = default(VisibleTGridRange);
                var leftTGrid = default(TGrid);
                var rightTGrid = default(TGrid);
                var rightMergeds = Enumerable.Empty<VisibleTGridRange>();

                var left = 0d;
                var right = 0d;
                var newLeftRemain = 0d;
                var newRightRemain = 0d;

                if (cur.Speed > 0)
                {
                    var calcLeftY = y - leftRemain;
                    left = Math.Max(calcLeftY, cur.Y);
                    //newLeftRemain = Math.Max(cur.Y - calcLeftY, 0);
                    newLeftRemain = cur.Y - calcLeftY;

                    var calcRightY = y + rightRemain;
                    right = Math.Min(next.Y, calcRightY);
                    //newRightRemain = Math.Max(calcRightY - next.Y, 0);
                    newRightRemain = calcRightY - next.Y;
                }
                else
                {
                    var calcLeftY = y + leftRemain;
                    left = Math.Min(calcLeftY, cur.Y);
                    newLeftRemain = Math.Max(-cur.Y + left, 0);

                    var calcRightY = y - rightRemain;
                    right = Math.Max(next.Y, calcRightY);
                    newRightRemain = Math.Max(next.Y - calcRightY, 0);
                }

                //计算在此变速段中能显示的范围leftTGrid/rightTGrid,也计算出剩余还需要显示的量newLeftRemain/newRightRemain
                //这里为了减轻大脑心智负担，还是按正反变速分开写吧
                if (cur.Speed > 0)
                {
                    leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed, tUnitLength));
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed, tUnitLength));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }
                else
                {
                    //问题是倒车时，left实际显示范围比用户指定的leftRemain还要大，因此实际上还得合并整个viewHeight
                    leftTGrid = (cur.TGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, (cur.Y - left)) / absSpeed, tUnitLength))) ?? TGrid.Zero;
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed, tUnitLength));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }

                if (newRightRemain >= 0 && newLeftRemain >= 0)
                    fullCheckSets.Add(posIdx);

                //Log.LogDebug($"{{{cur.TGrid}({cur.Y})  -->  {next.TGrid}({next.Y})}}  calc({leftRemain}|{y}|{rightRemain})  {{{leftTGrid}({left}){newLeftRemain}  -->  {rightTGrid}({right}){newRightRemain}}}");

                if (newLeftRemain > 0)
                {
                    //如果还有剩余，那么就说明还需要继续拿上一个变速段参与计算
                    if (posIdx > 0)
                        leftMergeds = CalcSegment(posIdx - 1, left, newLeftRemain, 0);
                    else
                    {
                        //如果当前是第一个变速段的话，那么也能很快计算出剩余newLeftRemain对应的可视范围
                        //这里假设第一个变速点的Speed是正向的
                        //但实际上，这个理论上不应该走到这里
                        var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed, tUnitLength));
                        overLeftTGrid = overLeftTGrid ?? TGrid.Zero;
                        leftMergeds = leftMergeds.Append(new VisibleTGridRange(overLeftTGrid, leftTGrid));
                    }
                }

                if (newRightRemain > 0)
                {
                    //如果还有剩余，那么就说明还需要继续拿下一个变速段参与计算
                    if (posIdx < list.Count - 2)
                        rightMergeds = CalcSegment(posIdx + 1, right, 0, newRightRemain);
                    else
                    {
                        var absNextSpeed = Math.Abs(next.Speed);
                        //如果当前是最后一个变速段的话，那么也能很快计算出剩余newRightRemain对应的可视范围
                        //这里假设最后一个变速点的Speed是正向的
                        var overRightTGrid = rightTGrid + (absNextSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed, tUnitLength));
                        rightMergeds = rightMergeds.Append(new VisibleTGridRange(rightTGrid, overRightTGrid));
                    }
                }

                var merged = leftMergeds.Append(curTGrid).Concat(rightMergeds);
                return merged;
            }

            IEnumerable<VisibleTGridRange> _internal()
            {
                var minY = 0d;
                var maxY = 0d;
                var last = default(SoflanPoint);

                //判断是否有变速
                if (list.Count > 1)
                {
                    /*
                    //如果有变速，那么就通过各个变速区间对比和计算
                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        last = list[i];
                        var next = list[i + 1];

                        minY = Math.Min(last.Y, next.Y);
                        maxY = Math.Max(last.Y, next.Y);

                        //检查可视范围是否和这个变速段范围有相交
                        if ((minY <= currentY && currentY <= maxY) || actualViewMaxY >= minY && maxY >= actualViewMinY)
                        {
                            MainCalledCount++;
                            //如果有相交，那么说明这个变速段部分内容是需要显示的，那么就计算出这部分需要显示的范围
                            var mergeds = CalcSegment(i, currentY, preOffset / scale, actualViewHeight - preOffset / scale);
                            foreach (var range in mergeds)
                                yield return range;
                        }
                    }
                    */
                    /*
                     新的优化实现：
                      1. 获取要被扫描的变速段
                      2. 按时间轴排序
                      3. 先从左到右，只向右扫描一边，如果出现某个变速段能被全扫完,那就标记这个。下次再扫到这个变速段就直接返回(毕竟都已经扫完了)
                      4. 同理，反过来再扫一遍
                      5. 完成
                     */
                    using var _d2 = segments.Query(currentY, currentY)
                        .Concat(segments.Query(actualViewMinY, actualViewMaxY))
                        .Distinct()
                        .OrderBy(x => x.curIdx)
                        .ToListWithObjectPool(out var querySegments);

                    var scanLeftLength = actualViewHeight - actualPreOffset;
                    for (int i = querySegments.Count - 1; i >= 0; i--)
                    {
                        var mergeds = CalcSegment(querySegments[i].curIdx, currentY, 0, scanLeftLength);
                        foreach (var range in mergeds)
                            yield return range;
                    }

                    fullCheckSets.Clear();

                    var scanRightLength = actualPreOffset;
                    for (int i = 0; i < querySegments.Count; i++)
                    {
                        var mergeds = CalcSegment(querySegments[i].curIdx, currentY, scanRightLength, 0);
                        foreach (var range in mergeds)
                            yield return range;
                    }

                    /*
                    foreach (var seg in querySegments)
                    {
                        if (prevMaxTGrid is not null && prevMaxTGrid > seg.next.TGrid)
                            continue;
                        prevMaxTGrid = null;

                        MainCalledCount++;
                        var i = seg.curIdx;
                        //如果有相交，那么说明这个变速段部分内容是需要显示的，那么就计算出这部分需要显示的范围
                        var mergeds = CalcSegment(i, currentY, actualPreOffset, actualViewHeight - actualPreOffset);
                        foreach (var range in mergeds)
                        {
                            if (prevMaxTGrid is null || prevMaxTGrid < range.maxTGrid)
                                prevMaxTGrid = range.maxTGrid;
                            yield return range;
                        }
                    }
                    */
                    last = list.Last();

                    if (last.Y <= minY)
                    {
                        //todo 这里我忘记为啥要写了，出了bug再看
                        var gridOffset = last.Bpm.LengthConvertToOffset(actualViewMinY - last.Y, tUnitLength);
                        var minTGrid = last.TGrid + gridOffset;

                        gridOffset = last.Bpm.LengthConvertToOffset(actualViewMaxY - last.Y, tUnitLength);
                        var maxTGrid = last.TGrid + gridOffset;

                        var range = new VisibleTGridRange(MathUtils.Min(minTGrid, maxTGrid), MathUtils.Max(minTGrid, maxTGrid));
                        yield return range;
                    }
                    else if (currentY >= last.Y)
                    {
                        //如果已经超过了最后一个变速点，那么这里也要计算超出的范围
                        //为了减轻我的心智负担，这坨内容和CalcSegment()大差不多，但不需要next参数了
                        var absSpeed = Math.Abs(last.Speed);

                        var y = currentY;
                        var leftRemain = actualPreOffset;
                        var rightRemain = actualViewHeight - actualPreOffset;

                        if (last.Speed > 0)
                        {
                            var calcLeftY = y - leftRemain;
                            var left = Math.Max(calcLeftY, last.Y);
                            var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed, tUnitLength));

                            var calcRightY = y + rightRemain;
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((calcRightY - last.Y) / absSpeed, tUnitLength));

                            var curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                            yield return curTGrid;
                        }
                        else
                        {
                            var calcLeftY = y + leftRemain;
                            var left = Math.Min(calcLeftY, last.Y);
                            var leftTGrid = (last.TGrid - (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, (last.Y - left)) / absSpeed, tUnitLength))) ?? TGrid.Zero;

                            var calcRightY = y - rightRemain;
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((last.Y - calcRightY) / absSpeed, tUnitLength));

                            var curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                            yield return curTGrid;
                        }
                    }
                }
                else
                {
                    //如果没有变速，那么就简单计算和处理咯~
                    last = list[0];
                    var absSpeed = Math.Abs(last.Speed);

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(0, actualViewMinY);
                        var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(left / absSpeed, tUnitLength));

                        var right = left + actualViewHeight;
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(right / absSpeed, tUnitLength));

                        yield return new(leftTGrid, rightTGrid);
                    }
                    else
                    {
                        //理论上不应该会走到这
                    }
                }
            }

            //尽量合并得到的VisibleTGridRange
            return TryMerge(_internal());
        }
    }

    #endregion
}
