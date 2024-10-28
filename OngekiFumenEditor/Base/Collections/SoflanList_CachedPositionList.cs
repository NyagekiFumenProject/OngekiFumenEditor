using IntervalTree;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;

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

        private int cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);

        private List<SoflanPoint> cachedSoflanPositionList_DesignMode = new();
        private List<SoflanPoint> cachedSoflanPositionList_PreviewMode = new();

        public record VisibleTGridRange(TGrid minTGrid, TGrid maxTGrid)
        {
            public bool TryMerge(VisibleTGridRange another, out VisibleTGridRange mergedResult)
            {
                mergedResult = Merge(another);
                return mergedResult != default;
            }

            public VisibleTGridRange Merge(VisibleTGridRange another)
            {
                if ((minTGrid <= another.minTGrid && another.minTGrid <= maxTGrid) ||
                    another.minTGrid <= minTGrid && minTGrid <= another.maxTGrid)
                    return new(MathUtils.Min(minTGrid, another.minTGrid), MathUtils.Max(maxTGrid, another.maxTGrid));
                return default;
            }
        }
        public record SoflanSegment(int curIdx, SoflanPoint cur, SoflanPoint next);

        private IIntervalTree<double, SoflanSegment> cachePostionList_PreviewMode;

        [Flags]
        private enum ChgEvt
        {
            None = 0,
            BpmChanged = 1,
            SoflanBegan = 2,
            SoflanEnded = 4,
            SoflanChanged = SoflanBegan | SoflanEnded
        }

        public IEnumerable<(TGrid TGrid, double speed, BPMChange curBpm)> GetCalculatableEvents(BpmList bpmList, bool isDesignModel)
        {
            var sortList = new List<(ITimelineObject timeline, ChgEvt evt)>();
            foreach (var timelineObject in CollectionHelper.MergeTwoSortedCollections<ITimelineObject, TGrid>(x => x.TGrid, this, bpmList))
            {
                switch (timelineObject)
                {
                    case IDurationSoflan durationEvt:
                        var itor = durationEvt.GenerateKeyframeSoflans().GetEnumerator();
                        if (itor.MoveNext())
                        {
                            var init = itor.Current;
                            if (itor.MoveNext())
                            {
                                sortList.Add((init, ChgEvt.SoflanBegan));
                                var prev = itor.Current;
                                while (itor.MoveNext())
                                {
                                    sortList.Add((prev, ChgEvt.SoflanChanged));
                                    prev = itor.Current;
                                }
                                sortList.Add((prev, ChgEvt.SoflanEnded));
                            }
                            else
                            {
                                sortList.Add((init, ChgEvt.SoflanChanged));
                            }
                        }
                        break;
                    case IKeyframeSoflan keyframeEvt:
                        sortList.Add((keyframeEvt, ChgEvt.SoflanChanged));
                        break;
                    case BPMChange bpmEvt:
                        sortList.Add((bpmEvt, ChgEvt.BpmChanged));
                        break;
                    default:
                        throw new Exception($"Not support object for GetCalculatableEvents(): {timelineObject}");
                }
            }

            IEnumerable<ITimelineObject> filter(IEnumerable<(ITimelineObject timeline, ChgEvt evt)> x)
            {
                var soflan = default(ITimelineObject);

                foreach (var item in x)
                {
                    switch (item.timeline)
                    {
                        case BPMChange:
                            yield return item.timeline;
                            break;
                        case IKeyframeSoflan:
                            if (item.evt == ChgEvt.SoflanEnded)
                                soflan ??= item.timeline;
                            else
                                soflan = item.timeline;
                            break;
                        default:
                            break;
                    }
                }

                if (soflan != null)
                    yield return soflan;
            }

            var groupEvents = sortList.GroupBy(x => x.timeline.TGrid);
            var combineEvents = groupEvents.SelectMany(filter).OrderBy(x => x.TGrid);

            IEnumerable<(TGrid TGrid, double speed, BPMChange bpm)> visit()
            {
                double GetSpeed(ISoflan soflan) => isDesignModel ? soflan.SpeedInEditor : soflan.Speed;
                var firstSoflan = this.FirstOrDefault();
                if (firstSoflan != null && firstSoflan.TGrid > TGrid.Zero)
                    firstSoflan = default;

                (TGrid TGrid, double speed, BPMChange bpm) currentState =
                    (TGrid.Zero, firstSoflan is null ? 1 : GetSpeed(firstSoflan), bpmList.GetBpm(TGrid.Zero));

                foreach (var item in combineEvents)
                {
                    var curTGrid = item.TGrid;

                    if (curTGrid != currentState.TGrid)
                    {
                        yield return currentState;
                        currentState.TGrid = curTGrid;
                    }

                    switch (item)
                    {
                        case BPMChange curBpmChange:
                            currentState.bpm = curBpmChange;
                            break;
                        case IKeyframeSoflan soflan:
                            currentState.speed = GetSpeed(soflan);
                            break;
                        default:
                            break;
                    }
                }

                yield return currentState;
            }

            var r = visit();
            return r;
        }

        private void UpdateCachedSoflanPositionList(BpmList bpmList, List<SoflanPoint> list, bool isDesignMode)
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

                var len = MathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.curBpm.BPM);

                var scaledLen = len * (isDesignMode ? Math.Abs(prevEvent.speed) : prevEvent.speed);

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

        private object locker = new object();

        private void CheckAndUpdateSoflanPositionList(BpmList bpmList)
        {
            var hash = bpmList.cachedBpmContentHash;

            if (cachedSoflanListCacheHash != hash)
            {
                lock (locker)
                {
                    if (cachedSoflanListCacheHash != hash)
                    {
                        Log.LogDebug("recalculate all.");
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_DesignMode, true);
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_PreviewMode, false);
                        cachePostionList_PreviewMode = RebuildIntervalTreePositionList(cachedSoflanPositionList_PreviewMode);

                        cachedSoflanListCacheHash = hash;
                    }
                }
            }
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_DesignMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
            return cachedSoflanPositionList_DesignMode;
        }

        public IList<SoflanPoint> GetCachedSoflanPositionList_PreviewMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
            return cachedSoflanPositionList_PreviewMode;
        }

        public IIntervalTree<double, SoflanSegment> GetCachedSoflanSegment_PreviewMode(BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(bpmList);
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
        public IEnumerable<VisibleTGridRange> GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale)
        {
            currentY = currentY / scale;
            var actualViewHeight = viewHeight / scale;
            var actualPreOffset = preOffset / scale;
            var actualViewMinY = currentY - actualPreOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

            //fullCheckSets用来标记哪个变速段是完全被扫完的
            using var _d1 = ObjectPool<HashSet<int>>.GetWithUsingDisposable(out var fullCheckSets, out _);
            fullCheckSets.Clear();

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
                    newLeftRemain = Math.Min(leftRemain, cur.Y - calcLeftY);

                    var calcRightY = y + rightRemain;
                    right = Math.Min(next.Y, calcRightY);
                    //newRightRemain = Math.Max(calcRightY - next.Y, 0);
                    newRightRemain = Math.Min(rightRemain, calcRightY - next.Y);
                }
                else if (cur.Speed < 0)
                {
                    var calcLeftY = y + leftRemain;
                    left = Math.Min(calcLeftY, cur.Y);
                    newLeftRemain = Math.Min(-cur.Y + left, leftRemain);

                    var calcRightY = y - rightRemain;
                    right = Math.Max(next.Y, calcRightY);
                    newRightRemain = Math.Min(next.Y - calcRightY, rightRemain);
                }
                else
                {
                    newLeftRemain = leftRemain;
                    newRightRemain = rightRemain;
                }

                //计算在此变速段中能显示的范围leftTGrid/rightTGrid,也计算出剩余还需要显示的量newLeftRemain/newRightRemain
                //这里为了减轻大脑心智负担，还是按正反变速分开写吧
                if (cur.Speed > 0)
                {
                    leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed));
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }
                else if (cur.Speed < 0)
                {
                    //问题是倒车时，left实际显示范围比用户指定的leftRemain还要大，因此实际上还得合并整个viewHeight
                    leftTGrid = (cur.TGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, (cur.Y - left)) / absSpeed))) ?? TGrid.Zero;
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed));

                    curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                }
                else
                {
                    //Speed = 0时就简单了~
                    leftTGrid = cur.TGrid;
                    rightTGrid = next.TGrid;

                    left = cur.Y;
                    right = next.Y;

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
                        var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed));
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
                        var overRightTGrid = rightTGrid + (absNextSpeed == 0 ? GridOffset.Zero : next.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed));
                        rightMergeds = rightMergeds.Append(new VisibleTGridRange(rightTGrid, overRightTGrid));
                    }
                }

                var merged = leftMergeds.Append(curTGrid).Concat(rightMergeds);
                return merged;
            }

            IEnumerable<VisibleTGridRange> _internal()
            {
                //判断是否有变速
                if (list.Count > 1)
                {
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

                    var last = list.Last();

                    if (currentY >= last.Y)
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
                            var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed));

                            var calcRightY = y + rightRemain;
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((calcRightY - last.Y) / absSpeed));

                            var curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                            yield return curTGrid;
                        }
                        else
                        {
                            var calcLeftY = y + leftRemain;
                            var left = Math.Min(calcLeftY, last.Y);
                            var leftTGrid = (last.TGrid - (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, (last.Y - left)) / absSpeed))) ?? TGrid.Zero;

                            var calcRightY = y - rightRemain;
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((last.Y - calcRightY) / absSpeed));

                            var curTGrid = new VisibleTGridRange(leftTGrid, rightTGrid);
                            yield return curTGrid;
                        }
                    }
                }
                else
                {
                    //如果没有变速，那么就简单计算和处理咯~
                    var last = list[0];
                    var absSpeed = Math.Abs(last.Speed);

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(0, actualViewMinY);
                        var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(left / absSpeed));

                        var right = left + actualViewHeight;
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(right / absSpeed));

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

        public double CalculateSpeed(BpmList bpmList, TGrid t)
        {
            var soflan = GetCachedSoflanPositionList_PreviewMode(bpmList).LastOrDefaultByBinarySearch(t, x => x.TGrid);
            return soflan.Speed;
        }

        public IEnumerable<Soflan> GenerateDurationSoflans(BpmList bpmList)
        {
            var list = GetCachedSoflanPositionList_PreviewMode(bpmList).Select(x => new
            {
                x.TGrid,
                x.Speed
            }).OrderBy(x => x.TGrid)
            .ToArray();

            for (var i = 0; i < list.Length - 1; i++)
            {
                yield return new Soflan()
                {
                    TGrid = list[i].TGrid,
                    Speed = (float)list[i].Speed,
                    EndTGrid = list[i + 1].TGrid,
                };
            }
        }

        public IEnumerable<KeyframeSoflan> GenerateKeyframeSoflans(BpmList bpmList)
        {
            var list = GetCachedSoflanPositionList_PreviewMode(bpmList).Select(x => new
            {
                x.TGrid,
                x.Speed
            }).OrderBy(x => x.TGrid)
            .DistinctContinuousBy(x => x.Speed);

            foreach (var item in list)
            {
                yield return new KeyframeSoflan()
                {
                    TGrid = item.TGrid,
                    Speed = (float)item.Speed,
                };
            }
        }
    }

    #endregion
}
