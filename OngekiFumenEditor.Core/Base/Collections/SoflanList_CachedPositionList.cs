using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.Collections.Base.RangeTree;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
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
                    return new(minTGrid <= another.minTGrid ? minTGrid : another.minTGrid, maxTGrid >= another.maxTGrid ? maxTGrid : another.maxTGrid);
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

            IEnumerable<ITimelineObject> Filter(IEnumerable<(ITimelineObject timeline, ChgEvt evt)> source)
            {
                var soflan = default(ITimelineObject);

                foreach (var item in source)
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
                    }
                }

                if (soflan != null)
                    yield return soflan;
            }

            var groupEvents = sortList.GroupBy(x => x.timeline.TGrid);
            var combineEvents = groupEvents.SelectMany(Filter).OrderBy(x => x.TGrid);

            IEnumerable<(TGrid TGrid, double speed, BPMChange bpm)> Visit()
            {
                double GetSpeed(ISoflan soflan) => isDesignModel ? soflan.SpeedInEditor : soflan.Speed;
                var firstSoflan = this.FirstOrDefault();
                if (firstSoflan != null && firstSoflan.TGrid > TGrid.Zero)
                    firstSoflan = default;

                (TGrid TGrid, double speed, BPMChange bpm) currentState =
                    (TGrid.Zero, firstSoflan is null ? 1 : GetSpeed(firstSoflan), bpmList.GetBpm(TGrid.Zero));

                foreach (var item in combineEvents)
                {
                    if (item.TGrid != currentState.TGrid)
                    {
                        yield return currentState;
                        currentState.TGrid = item.TGrid;
                    }

                    switch (item)
                    {
                        case BPMChange curBpmChange:
                            currentState.bpm = curBpmChange;
                            break;
                        case IKeyframeSoflan soflan:
                            currentState.speed = GetSpeed(soflan);
                            break;
                    }
                }

                yield return currentState;
            }

            return Visit();
        }

        private void UpdateCachedSoflanPositionList(BpmList bpmList, List<SoflanPoint> list, bool isDesignMode)
        {
            list.Clear();

            using var itor = GetCalculatableEvents(bpmList, isDesignMode).GetEnumerator();
            if (!itor.MoveNext())
                return;

            var currentY = 0d;
            var prevEvent = itor.Current;

            while (itor.MoveNext())
            {
                var curEvent = itor.Current;
                var len = BpmMathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.curBpm.BPM);
                var scaledLen = len * (isDesignMode ? Math.Abs(prevEvent.speed) : prevEvent.speed);

                list.Add(new(currentY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));

                currentY += scaledLen;
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

                tree.Add(Math.Min(prev.Y, next.Y), Math.Max(prev.Y, next.Y), new(i, prev, next));
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

        public IEnumerable<VisibleTGridRange> GetVisibleRanges_PreviewMode(double currentY, double viewHeight, double preOffset, BpmList bpmList, double scale)
        {
            currentY /= scale;
            var actualViewHeight = viewHeight / scale;
            var actualPreOffset = preOffset / scale;
            var actualViewMinY = currentY - actualPreOffset;
            var actualViewMaxY = actualViewMinY + actualViewHeight;

            var list = GetCachedSoflanPositionList_PreviewMode(bpmList);
            var segments = GetCachedSoflanSegment_PreviewMode(bpmList);

            var fullCheckSets = new HashSet<int>();

            IEnumerable<VisibleTGridRange> TryMerge(IEnumerable<VisibleTGridRange> sortedList)
            {
                using var itor = sortedList.OrderBy(x => x.minTGrid).GetEnumerator();
                if (!itor.MoveNext())
                    yield break;

                var cur = itor.Current;
                while (itor.MoveNext())
                {
                    var next = itor.Current;
                    if (next.minTGrid <= cur.maxTGrid)
                        cur = new(cur.minTGrid <= next.minTGrid ? cur.minTGrid : next.minTGrid, cur.maxTGrid >= next.maxTGrid ? cur.maxTGrid : next.maxTGrid);
                    else
                    {
                        yield return cur;
                        cur = next;
                    }
                }

                yield return cur;
            }

            IEnumerable<VisibleTGridRange> CalcSegment(int posIdx, double y, double leftRemain, double rightRemain)
            {
                if (fullCheckSets.Contains(posIdx))
                    return Enumerable.Empty<VisibleTGridRange>();

                var cur = list[posIdx];
                var next = list[posIdx + 1];
                var absSpeed = Math.Abs(cur.Speed);

                var leftMergeds = Enumerable.Empty<VisibleTGridRange>();
                var rightMergeds = Enumerable.Empty<VisibleTGridRange>();
                var left = 0d;
                var right = 0d;
                var newLeftRemain = 0d;
                var newRightRemain = 0d;
                var leftTGrid = default(TGrid);
                var rightTGrid = default(TGrid);

                if (cur.Speed > 0)
                {
                    var calcLeftY = y - leftRemain;
                    left = Math.Max(calcLeftY, cur.Y);
                    newLeftRemain = Math.Min(leftRemain, cur.Y - calcLeftY);

                    var calcRightY = y + rightRemain;
                    right = Math.Min(next.Y, calcRightY);
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

                VisibleTGridRange curRange;
                if (cur.Speed > 0)
                {
                    leftTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((left - cur.Y) / absSpeed));
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((right - cur.Y) / absSpeed));
                    curRange = new(leftTGrid, rightTGrid);
                }
                else if (cur.Speed < 0)
                {
                    leftTGrid = (cur.TGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, cur.Y - left) / absSpeed))) ?? TGrid.Zero;
                    rightTGrid = cur.TGrid + (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset((cur.Y - right) / absSpeed));
                    curRange = new(leftTGrid, rightTGrid);
                }
                else
                {
                    leftTGrid = cur.TGrid;
                    rightTGrid = next.TGrid;
                    left = cur.Y;
                    right = next.Y;
                    curRange = new(leftTGrid, rightTGrid);
                }

                if (newRightRemain >= 0 && newLeftRemain >= 0)
                    fullCheckSets.Add(posIdx);

                if (newLeftRemain > 0)
                {
                    if (posIdx > 0)
                        leftMergeds = CalcSegment(posIdx - 1, left, newLeftRemain, 0);
                    else
                    {
                        var overLeftTGrid = leftTGrid - (absSpeed == 0 ? GridOffset.Zero : cur.Bpm.LengthConvertToOffset(newLeftRemain / absSpeed));
                        leftMergeds = leftMergeds.Append(new VisibleTGridRange(overLeftTGrid ?? TGrid.Zero, leftTGrid));
                    }
                }

                if (newRightRemain > 0)
                {
                    if (posIdx < list.Count - 2)
                        rightMergeds = CalcSegment(posIdx + 1, right, 0, newRightRemain);
                    else
                    {
                        var absNextSpeed = Math.Abs(next.Speed);
                        var overRightTGrid = rightTGrid + (absNextSpeed == 0 ? GridOffset.Zero : next.Bpm.LengthConvertToOffset(newRightRemain / absNextSpeed));
                        rightMergeds = rightMergeds.Append(new VisibleTGridRange(rightTGrid, overRightTGrid));
                    }
                }

                return leftMergeds.Append(curRange).Concat(rightMergeds);
            }

            IEnumerable<VisibleTGridRange> CoreQuery()
            {
                if (list.Count > 1)
                {
                    var querySegments = segments.Query(currentY, currentY)
                        .Concat(segments.Query(actualViewMinY, actualViewMaxY))
                        .Distinct()
                        .OrderBy(x => x.curIdx)
                        .ToList();

                    var scanLeftLength = actualViewHeight - actualPreOffset;
                    for (int i = querySegments.Count - 1; i >= 0; i--)
                    {
                        foreach (var range in CalcSegment(querySegments[i].curIdx, currentY, 0, scanLeftLength))
                            yield return range;
                    }

                    fullCheckSets.Clear();

                    var scanRightLength = actualPreOffset;
                    for (int i = 0; i < querySegments.Count; i++)
                    {
                        foreach (var range in CalcSegment(querySegments[i].curIdx, currentY, scanRightLength, 0))
                            yield return range;
                    }

                    var last = list.Last();
                    if (currentY >= last.Y)
                    {
                        var absSpeed = Math.Abs(last.Speed);
                        var leftRemain = actualPreOffset;
                        var rightRemain = actualViewHeight - actualPreOffset;

                        if (last.Speed > 0)
                        {
                            var left = Math.Max(currentY - leftRemain, last.Y);
                            var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left - last.Y) / absSpeed));
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((currentY + rightRemain - last.Y) / absSpeed));
                            yield return new(leftTGrid, rightTGrid);
                        }
                        else
                        {
                            var left = Math.Min(currentY + leftRemain, last.Y);
                            var leftTGrid = (last.TGrid - (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(Math.Max(actualViewHeight, last.Y - left) / absSpeed))) ?? TGrid.Zero;
                            var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((last.Y - (currentY - rightRemain)) / absSpeed));
                            yield return new(leftTGrid, rightTGrid);
                        }
                    }
                }
                else
                {
                    var last = list[0];
                    if (last.Speed > 0)
                    {
                        var absSpeed = Math.Abs(last.Speed);
                        var left = Math.Max(0, actualViewMinY);
                        var leftTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset(left / absSpeed));
                        var rightTGrid = last.TGrid + (absSpeed == 0 ? GridOffset.Zero : last.Bpm.LengthConvertToOffset((left + actualViewHeight) / absSpeed));
                        yield return new(leftTGrid, rightTGrid);
                    }
                }
            }

            return TryMerge(CoreQuery());
        }

        public double CalculateSpeed(BpmList bpmList, TGrid t)
        {
            var soflan = LastOrDefaultByBinarySearch(GetCachedSoflanPositionList_PreviewMode(bpmList), t, x => x.TGrid);
            return soflan.Speed;
        }

        public IEnumerable<Soflan> GenerateDurationSoflans(BpmList bpmList, int soflanGroup)
        {
            var list = GetCachedSoflanPositionList_PreviewMode(bpmList)
                .Select(x => new { x.TGrid, x.Speed })
                .OrderBy(x => x.TGrid)
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
            var list = DistinctContinuousBy(GetCachedSoflanPositionList_PreviewMode(bpmList)
                .Select(x => new { x.TGrid, x.Speed })
                .OrderBy(x => x.TGrid), x => x.Speed);

            foreach (var item in list)
            {
                yield return new KeyframeSoflan()
                {
                    TGrid = item.TGrid,
                    Speed = (float)item.Speed,
                };
            }
        }

        private static T LastOrDefaultByBinarySearch<T, TKey>(IList<T> source, TKey value, Func<T, TKey> keySelect)
            where TKey : IComparable<TKey>
        {
            var lo = 0;
            var hi = source.Count - 1;

            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var key = keySelect(source[i]);
                var order = key.CompareTo(value);

                if (order <= 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return source[Math.Max(0, hi)];
        }

        private static IEnumerable<T> DistinctContinuousBy<T, TKey>(IEnumerable<T> collection, Func<T, TKey> keySelect)
        {
            using var itor = collection.GetEnumerator();
            var isFirst = true;
            var prevKey = default(TKey);
            var comparer = EqualityComparer<TKey>.Default;

            while (itor.MoveNext())
            {
                var value = itor.Current;
                var key = keySelect(value);

                if (isFirst || !comparer.Equals(prevKey, key))
                {
                    yield return value;
                    isFirst = false;
                }

                prevKey = key;
            }
        }
    }
}
