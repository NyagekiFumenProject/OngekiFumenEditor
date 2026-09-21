using OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Base.Collections
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

        private int cachedSoflanPositionBpmVersion = NonceGenerator.Next();

        private List<SoflanPoint> cachedSoflanPositionList_DesignMode = new();
        private List<SoflanPoint> cachedSoflanPositionList_PreviewMode = new();

        public readonly record struct VisibleTGridRange(TGrid minTGrid, TGrid maxTGrid);
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
                list.Add(new(0, TGrid.Zero, 1.0d, bpmList.FirstOrDefault()));
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
            var version = bpmList.ContentVersion;

            if (NeedsRebuildSoflanPositionList(version))
            {
                lock (locker)
                {
                    if (NeedsRebuildSoflanPositionList(version))
                    {
                        //Log.LogDebug("recalculate all.");
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_DesignMode, true);
                        UpdateCachedSoflanPositionList(bpmList, cachedSoflanPositionList_PreviewMode, false);
                        cachePostionList_PreviewMode = RebuildIntervalTreePositionList(cachedSoflanPositionList_PreviewMode);

                        cachedSoflanPositionBpmVersion = version;
                    }
                }
            }
        }

        /// <summary>
        /// 缓存是否已失效。入参 <paramref name="version"/> 是锁定前读到的令牌，
        /// 与旧实现（比较进入时算出的内容哈希）一致：重算期间发生的变更会在下次调用时再触发一次重算。
        /// </summary>
        private bool NeedsRebuildSoflanPositionList(int version)
            => cachedSoflanPositionBpmVersion != version;

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
        public IPooledList<VisibleTGridRange> GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale)
        {
            currentY /= scale;
            var actualViewHeight = viewHeight / scale;
            var actualPreOffset = preOffset / scale;
            var actualViewMinY = currentY - actualPreOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

            //fullCheckSets用来标记哪个变速段是完全被扫完的
            using var fullCheckSets = ObjectPool.GetPooledSet<int>();
            var rawResults = ObjectPool.GetPooledList<VisibleTGridRange>();
            try
            {
                DoCalcSegment(list, segments, rawResults, fullCheckSets,
                    currentY, actualViewHeight, actualPreOffset, actualViewMinY, actualViewMaxY);

                //尽量合并得到的VisibleTGridRange
                var result = ObjectPool.GetPooledList<VisibleTGridRange>();
                try
                {
                    MergeOverlapped(rawResults, result);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
            finally
            {
                rawResults.Dispose();
            }
        }

        private sealed class VisibleTGridRangeByMinComparer : IComparer<VisibleTGridRange>
        {
            public static readonly VisibleTGridRangeByMinComparer Instance = new();

            public int Compare(VisibleTGridRange x, VisibleTGridRange y) => x.minTGrid.CompareTo(y.minTGrid);
        }

        private static void MergeOverlapped(IPooledList<VisibleTGridRange> rawResults, IPooledList<VisibleTGridRange> output)
        {
            if (rawResults.Count == 0)
                return;

            if (rawResults.Count == 1)
            {
                output.Add(rawResults[0]);
                return;
            }

            rawResults.Sort(VisibleTGridRangeByMinComparer.Instance);

            var cur = rawResults[0];
            for (var i = 1; i < rawResults.Count; i++)
            {
                var next = rawResults[i];
                if (next.minTGrid <= cur.maxTGrid)
                {
                    //combinable
                    var newMin = cur.minTGrid <= next.minTGrid ? cur.minTGrid : next.minTGrid;
                    var newMax = cur.maxTGrid >= next.maxTGrid ? cur.maxTGrid : next.maxTGrid;
                    cur = new VisibleTGridRange(newMin, newMax);
                }
                else
                {
                    output.Add(cur);
                    cur = next;
                }
            }

            output.Add(cur);
        }

        private static void DoCalcSegment(
            IList<SoflanPoint> list,
            IIntervalTree<double, SoflanSegment> segments,
            IPooledList<VisibleTGridRange> rawResults,
            IPooledSet<int> fullCheckSets,
            double currentY,
            double actualViewHeight,
            double actualPreOffset,
            double actualViewMinY,
            double actualViewMaxY)
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
                using var queryIdx = ObjectPool.GetPooledList<int>();
                {
                    using var rawSegments = ObjectPool.GetPooledList<SoflanSegment>();
                    segments.QueryInto(actualViewMinY, actualViewMaxY, rawSegments);
                    // IntervalTree 内部按互斥分区(left/inner/right)构建，每个 SoflanSegment 仅出现一次，无需去重
                    for (var i = 0; i < rawSegments.Count; i++)
                        queryIdx.Add(rawSegments[i].curIdx);
                }

                queryIdx.Sort(Comparer<int>.Default);

                var scanLeftLength = actualViewHeight - actualPreOffset;
                for (var i = queryIdx.Count - 1; i >= 0; i--)
                    CalcSegmentRecursive(queryIdx[i], currentY, 0, scanLeftLength,
                        list, rawResults, fullCheckSets, actualViewHeight);

                fullCheckSets.Clear();

                var scanRightLength = actualPreOffset;
                for (var i = 0; i < queryIdx.Count; i++)
                    CalcSegmentRecursive(queryIdx[i], currentY, scanRightLength, 0,
                        list, rawResults, fullCheckSets, actualViewHeight);

                var last = list[list.Count - 1];
                if (currentY >= last.Y)
                {
                    //如果已经超过了最后一个变速点，那么这里也要计算超出的范围
                    //为了减轻我的心智负担，这坨内容和CalcSegment()大差不多，但不需要next参数了
                    var absSpeed = Math.Abs(last.Speed);
                    var leftRemain = actualPreOffset;
                    var rightRemain = actualViewHeight - actualPreOffset;

                    if (last.Speed > 0)
                    {
                        var left = Math.Max(currentY - leftRemain, last.Y);
                        var leftTGrid = last.TGrid + (absSpeed == 0
                            ? GridOffset.Zero
                            : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed));
                        var rightTGrid = last.TGrid + (absSpeed == 0
                            ? GridOffset.Zero
                            : last.Bpm.LengthConvertToOffset((currentY + rightRemain - last.Y) / absSpeed));
                        rawResults.Add(new VisibleTGridRange(leftTGrid, rightTGrid));
                    }
                    else
                    {
                        var left = Math.Min(currentY + leftRemain, last.Y);
                        var leftTGrid = (last.TGrid - (absSpeed == 0
                            ? GridOffset.Zero
                            : last.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, last.Y - left) / absSpeed))) ?? TGrid.Zero;
                        var rightTGrid = last.TGrid + (absSpeed == 0
                            ? GridOffset.Zero
                            : last.Bpm.LengthConvertToOffset((last.Y - (currentY - rightRemain)) / absSpeed));
                        rawResults.Add(new VisibleTGridRange(leftTGrid, rightTGrid));
                    }
                }
            }
            else
            {
                //如果没有变速，那么就简单计算和处理咯~
                var last = list[0];
                if (last.Speed > 0)
                {
                    var absSpeed = Math.Abs(last.Speed);
                    var left = Math.Max(0, actualViewMinY);
                    var leftTGrid = last.TGrid + (absSpeed == 0
                        ? GridOffset.Zero
                        : last.Bpm.LengthConvertToOffset(left / absSpeed));
                    var rightTGrid = last.TGrid + (absSpeed == 0
                        ? GridOffset.Zero
                        : last.Bpm.LengthConvertToOffset((left + actualViewHeight) / absSpeed));
                    rawResults.Add(new VisibleTGridRange(leftTGrid, rightTGrid));
                }
                else
                {
                    //理论上不应该会走到这
                }
            }
        }

        private static void CalcSegmentRecursive(
            int posIdx,
            double y,
            double leftRemain,
            double rightRemain,
            IList<SoflanPoint> list,
            IPooledList<VisibleTGridRange> rawResults,
            IPooledSet<int> fullCheckSets,
            double actualViewHeight)
        {
            if (fullCheckSets.Contains(posIdx))
                return;

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
            var cur = list[posIdx]; //当前变速信息
            var next = list[posIdx + 1];
            var absSpeed = Math.Abs(cur.Speed);

            var left = 0d;
            var right = 0d;
            var newLeftRemain = 0d;
            var newRightRemain = 0d;
            TGrid leftTGrid;
            TGrid rightTGrid;

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
            VisibleTGridRange curRange;
            if (cur.Speed > 0)
            {
                leftTGrid = cur.TGrid + (absSpeed == 0
                    ? GridOffset.Zero
                    : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed));
                rightTGrid = cur.TGrid + (absSpeed == 0
                    ? GridOffset.Zero
                    : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed));
                curRange = new VisibleTGridRange(leftTGrid, rightTGrid);
            }
            else if (cur.Speed < 0)
            {
                //问题是倒车时，left实际显示范围比用户指定的leftRemain还要大，因此实际上还得合并整个viewHeight
                leftTGrid = (cur.TGrid - (absSpeed == 0
                    ? GridOffset.Zero
                    : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, cur.Y - left) / absSpeed))) ?? TGrid.Zero;
                rightTGrid = cur.TGrid + (absSpeed == 0
                    ? GridOffset.Zero
                    : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed));
                curRange = new VisibleTGridRange(leftTGrid, rightTGrid);
            }
            else
            {
                //Speed = 0时就简单了~
                leftTGrid = cur.TGrid;
                rightTGrid = next.TGrid;
                left = cur.Y;
                right = next.Y;
                curRange = new VisibleTGridRange(leftTGrid, rightTGrid);
            }

            if (newRightRemain >= 0 && newLeftRemain >= 0)
                fullCheckSets.Add(posIdx);

            //Log.LogDebug($"{{{cur.TGrid}({cur.Y})  -->  {next.TGrid}({next.Y})}}  calc({leftRemain}|{y}|{rightRemain})  {{{leftTGrid}({left}){newLeftRemain}  -->  {rightTGrid}({right}){newRightRemain}}}");

            if (newLeftRemain > 0)
            {
                //如果还有剩余，那么就说明还需要继续拿上一个变速段参与计算
                if (posIdx > 0)
                {
                    CalcSegmentRecursive(posIdx - 1, left, newLeftRemain, 0,
                        list, rawResults, fullCheckSets, actualViewHeight);
                }
                else
                {
                    //如果当前是第一个变速段的话，那么也能很快计算出剩余newLeftRemain对应的可视范围
                    //这里假设第一个变速点的Speed是正向的
                    //但实际上，这个理论上不应该走到这里
                    var overLeftTGrid = leftTGrid - (absSpeed == 0
                        ? GridOffset.Zero
                        : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed));
                    rawResults.Add(new VisibleTGridRange(overLeftTGrid ?? TGrid.Zero, leftTGrid));
                }
            }

            rawResults.Add(curRange);

            if (newRightRemain > 0)
            {
                //如果还有剩余，那么就说明还需要继续拿下一个变速段参与计算
                if (posIdx < list.Count - 2)
                {
                    CalcSegmentRecursive(posIdx + 1, right, 0, newRightRemain,
                        list, rawResults, fullCheckSets, actualViewHeight);
                }
                else
                {
                    var absNextSpeed = Math.Abs(next.Speed);
                    //如果当前是最后一个变速段的话，那么也能很快计算出剩余newRightRemain对应的可视范围
                    //这里假设最后一个变速点的Speed是正向的
                    var overRightTGrid = rightTGrid + (absNextSpeed == 0
                        ? GridOffset.Zero
                        : next.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed));
                    rawResults.Add(new VisibleTGridRange(rightTGrid, overRightTGrid));
                }
            }
        }

        public double CalculateSpeed(BpmList bpmList, TGrid t)
        {
            var soflan = GetCachedSoflanPositionList_PreviewMode(bpmList).LastOrDefaultByBinarySearch(t, x => x.TGrid);
            return soflan.Speed;
        }

        public IEnumerable<Soflan> GenerateDurationSoflans(BpmList bpmList, int soflanGroup)
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
                    SoflanGroup = soflanGroup
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

