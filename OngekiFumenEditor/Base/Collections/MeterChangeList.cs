using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OngekiFumenEditor.Base.Collections
{
    public class MeterChangeList : IBinaryFindRangeEnumable<MeterChange, TGrid>
    {
        private MeterChange firstMeter = new MeterChange();
        private TGridSortList<MeterChange> changedMeterList = new();
        public MeterChange FirstMeter => firstMeter;

        public event Action OnChangedEvent;

        public MeterChangeList(IEnumerable<MeterChange> initBpmChanges = default)
        {
            OnChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<MeterChange>())
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent()
        {
            cachedMetListCacheHash = int.MinValue;
        }

        public void Add(MeterChange bpm)
        {
            changedMeterList.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ISelectableObject.IsSelected))
                OnChangedEvent?.Invoke();
        }

        public void SetFirstBpm(MeterChange firstBpm)
        {
            this.firstMeter = firstBpm;
            OnChangedEvent?.Invoke();
            firstBpm.PropertyChanged += OnBpmPropChanged;
        }

        public void Remove(MeterChange bpm)
        {
            if (bpm == firstMeter)
                throw new Exception($"BpmList can't delete firstBpm : {bpm}");
            changedMeterList.Remove(bpm);
            bpm.PropertyChanged -= OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        public IEnumerator<MeterChange> GetEnumerator()
        {
            yield return firstMeter;
            foreach (var item in changedMeterList.OrderBy(x => x.TGrid))
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public MeterChange GetMeter(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid <= time);

        public MeterChange GetPrevMeter(MeterChange time) => GetPrevMeter(time.TGrid);

        public MeterChange GetPrevMeter(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid < time);

        public MeterChange GetNextMeter(MeterChange bpm) => GetNextMeter(bpm.TGrid);

        public MeterChange GetNextMeter(TGrid time) => this.FirstOrDefault(bpm => time < bpm.TGrid);

        private List<(TimeSpan audioTime, TGrid startTGrid, MeterChange meterChange, BPMChange bpmChange)> cachedTimesignUniformPosition = new();
        private double cachedMetListCacheHash = int.MinValue;

        private void UpdateCachedAllTimeSignatureUniformPositionList(double tUnitLength, BpmList bpmList)
        {
            TGrid pickBiggerTGrid(ITimelineObject a, ITimelineObject b) => a.TGrid > b.TGrid ? a.TGrid : b.TGrid;

            cachedTimesignUniformPosition.Clear();

            //最初默认的
            cachedTimesignUniformPosition.Add((TimeSpan.FromMilliseconds(0), pickBiggerTGrid(FirstMeter, bpmList.FirstBpm), FirstMeter, bpmList.FirstBpm));

            var bpmUnitList = bpmList.GetCachedAllBpmUniformPositionList(tUnitLength);

            foreach (var meterChange in changedMeterList)
            {
                (var audioTime, var refBpm) = bpmUnitList.LastOrDefault(x => x.bpm.TGrid <= meterChange.TGrid);
                var meterY = audioTime + TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(refBpm, meterChange.TGrid, tUnitLength));
                cachedTimesignUniformPosition.Add((meterY, pickBiggerTGrid(meterChange, refBpm), meterChange, refBpm));
            }

            foreach ((var audioTime, var bpm) in bpmUnitList.Skip(1))
            {
                var meter = GetMeter(bpm.TGrid);
                cachedTimesignUniformPosition.Add((audioTime, pickBiggerTGrid(meter, bpm), meter, bpm));
            }

            cachedTimesignUniformPosition.SortBy(x => x.audioTime);

            //remove conflict meter position.
            var conflictGroups = cachedTimesignUniformPosition.GroupBy(x => x.audioTime).Where(x => x.Count() > 1);
            //using var disp = ObjectPool<HashSet<(double startY, MeterChange meterChange, BPMChange bpmChange)>>.GetWithUsingDisposable(out var removeSet,out _);
            var removeSet = new HashSet<(TimeSpan audioTime, TGrid startTGrid, MeterChange meterChange, BPMChange bpmChange)>();
            removeSet.Clear();
            foreach (var conflicts in conflictGroups)
            {
                removeSet.AddRange(conflicts.Skip(1));
                /*
                Log.LogDebug("detect meter positions conflict : ");
                foreach (var item in conflicts)
                {
                    Log.LogDebug($"* {item.startY} ({item.bpmChange}) ({item.meterChange})");
                }
                */
            }
            foreach (var item in removeSet)
                cachedTimesignUniformPosition.Remove(item);
        }

        public List<(TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm)> GetCachedAllTimeSignatureUniformPositionList(double tUnitLength, BpmList bpmList)
        {
            var hash = HashCode.Combine(tUnitLength, bpmList.cachedBpmContentHash);

            if (cachedMetListCacheHash != hash)
            {
                //Log.LogDebug("recalculate all time signatures.");
                UpdateCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
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
