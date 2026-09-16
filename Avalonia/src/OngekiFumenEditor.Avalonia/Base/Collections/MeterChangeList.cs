using System.Collections;
using System.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base.Collections
{
    public class MeterChangeList : IBinaryFindRangeEnumable<MeterChange, TGrid>
    {
        private MeterChange firstMeter;
        private TGridSortList<MeterChange> changedMeterList = new();
        public MeterChange FirstMeter => firstMeter;

        public int Count => 1 + changedMeterList.Count;

        public event Action OnChangedEvent;

        public MeterChangeList(IEnumerable<MeterChange> initMeterChanges = default)
        {
            SetFirstMeter(new MeterChange());

            OnChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initMeterChanges ?? Enumerable.Empty<MeterChange>())
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent()
        {
            cachedMetListCacheHash = int.MinValue;
        }

        public void Add(MeterChange meter)
        {
            changedMeterList.Add(meter);
            meter.PropertyChanged += OnMeterPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnMeterPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ISelectableObject.IsSelected))
                OnChangedEvent?.Invoke();
        }

        public void SetFirstMeter(MeterChange firstMet)
        {
            if (firstMeter is not null)
                firstMeter.PropertyChanged -= OnMeterPropChanged;
            firstMeter = firstMet;
            OnChangedEvent?.Invoke();
            firstMet.PropertyChanged += OnMeterPropChanged;
        }

        public bool Remove(MeterChange meter)
        {
            if (meter == firstMeter)
                throw new Exception($"MeterList can't delete firstMet : {meter},but you can use SetFirstMeter()");
            var r = changedMeterList.Remove(meter);
            if (r)
            {
                meter.PropertyChanged -= OnMeterPropChanged;
                OnChangedEvent?.Invoke();
            }
            return r;
        }

        public IEnumerator<MeterChange> GetEnumerator()
        {
            yield return firstMeter;
            // changedMeterList 是 TGridSortList<MeterChange>，插入即维持按 TGrid 升序，
            // 这里不需要再 OrderBy（旧实现在此每次枚举都重排一遍，PERF-DAT-010 / DAT-13）。
            foreach (var item in changedMeterList)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // 下面三个查询与旧实现的 Last/FirstOrDefault **语义逐项等价**：
        // 旧枚举序为 [firstMeter, changed(升序)]，故
        //   - 只要 changed 里存在满足项，“最后一个命中者”必定落在 changed 内 → 二分取边界即可；
        //   - changed 里不存在时，才可能退化为 firstMeter（单独判断），否则返回 null。
        // 前驱/后继改用 backing 的 lower/upper bound，替代旧实现「每次完整枚举（含重排）+ 线性扫描」。

        public MeterChange GetMeter(TGrid time)
        {
            // 最后一个 TGrid <= time
            var upper = changedMeterList.UpperBoundIndex(time);
            if (upper > 0)
                return changedMeterList[upper - 1];
            return firstMeter.TGrid <= time ? firstMeter : default;
        }

        public MeterChange GetPrevMeter(MeterChange time) => GetPrevMeter(time.TGrid);

        public MeterChange GetPrevMeter(TGrid time)
        {
            // 最后一个 TGrid < time（严格）
            var lower = changedMeterList.LowerBoundIndex(time);
            if (lower > 0)
                return changedMeterList[lower - 1];
            return firstMeter.TGrid < time ? firstMeter : default;
        }

        public MeterChange GetNextMeter(MeterChange meter) => GetNextMeter(meter.TGrid);

        public MeterChange GetNextMeter(TGrid time)
        {
            // 第一个 TGrid > time（严格）；枚举以 firstMeter 打头，故先看它
            if (firstMeter.TGrid > time)
                return firstMeter;
            var upper = changedMeterList.UpperBoundIndex(time);
            return upper < changedMeterList.Count ? changedMeterList[upper] : default;
        }

        private List<(TimeSpan audioTime, TGrid startTGrid, MeterChange meterChange, BPMChange bpmChange)> cachedTimesignUniformPosition = new();
        private double cachedMetListCacheHash = int.MinValue;

        [Flags]
        private enum ChgEvt
        {
            None = 0,
            MeterChanged = 1,
            BpmChanged = 2,
        }

        private void UpdateCachedAllTimeSignatureUniformPositionList(BpmList bpmList)
        {
            cachedTimesignUniformPosition.Clear();

            var sortList = new List<(ITimelineObject timeline, ChgEvt evt)>();
            foreach (var timelineObject in CollectionHelper.MergeTwoSortedCollections<ITimelineObject, TGrid>(x => x.TGrid, this, bpmList))
            {
                switch (timelineObject)
                {
                    case MeterChange meterChange:
                        sortList.Add((meterChange, ChgEvt.MeterChanged));
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
                foreach (var item in x)
                {
                    switch (item.timeline)
                    {
                        case BPMChange:
                            yield return item.timeline;
                            break;
                        case MeterChange:
                            yield return item.timeline;
                            break;
                        default:
                            break;
                    }
                }
            }

            var groupEvents = sortList.GroupBy(x => x.timeline.TGrid);
            var combineEvents = groupEvents.SelectMany(filter).OrderBy(x => x.TGrid);

            IEnumerable<(TGrid TGrid, MeterChange meter, BPMChange bpm)> visit()
            {
                var firstMeter = this.FirstOrDefault();

                (TGrid TGrid, MeterChange meter, BPMChange bpm) currentState =
                    (TGrid.Zero, firstMeter, bpmList.GetBpm(TGrid.Zero));

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
                        case MeterChange meter:
                            currentState.meter = meter;
                            break;
                        default:
                            break;
                    }
                }

                yield return currentState;
            }

            cachedTimesignUniformPosition.AddRange(visit().Select(x => (TGridCalculator.ConvertTGridToAudioTime(x.TGrid, bpmList), x.TGrid, x.meter, x.bpm)));
        }

        public List<(TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm)> GetCachedAllTimeSignatureUniformPositionList(BpmList bpmList)
        {
            var hash = HashCode.Combine(bpmList.cachedBpmContentHash);

            if (cachedMetListCacheHash != hash)
            {
                //Log.LogDebug("recalculate all time signatures.");
                UpdateCachedAllTimeSignatureUniformPositionList(bpmList);
                cachedMetListCacheHash = hash;
            }
            return cachedTimesignUniformPosition;
        }

        public (int minIndex, int maxIndex) BinaryFindRangeIndex(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<MeterChange, TGrid>)changedMeterList).BinaryFindRangeIndex(min, max);

        public IEnumerable<MeterChange> BinaryFindRange(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<MeterChange, TGrid>)changedMeterList).BinaryFindRange(min, max);

        public bool Contains(MeterChange obj)
            => ((IBinaryFindRangeEnumable<MeterChange, TGrid>)changedMeterList).Contains(obj);
    }
}

