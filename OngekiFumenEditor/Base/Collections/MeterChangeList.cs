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

namespace OngekiFumenEditor.Base.Collections
{
    public class MeterChangeList : IEnumerable<MeterChange>
    {
        private MeterChange firstMeter = new MeterChange();
        private List<MeterChange> changedMeterList = new List<MeterChange>();
        public MeterChange FirstMeter => firstMeter;

        public event Action OnChangedEvent;

        public MeterChangeList(IEnumerable<MeterChange> initBpmChanges = default)
        {
            OnChangedEvent += BpmList_OnChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<MeterChange>())
                Add(item);
        }

        private void BpmList_OnChangedEvent()
        {
            cachedBpmTUnitLength = int.MinValue;
        }

        public void Add(MeterChange bpm)
        {
            changedMeterList.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
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

        public void Sort() => changedMeterList.Sort();

        public MeterChange GetMeter(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid <= time);

        public MeterChange GetPrevMeter(MeterChange time) => GetPrevMeter(time.TGrid);

        public MeterChange GetPrevMeter(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid < time);

        public MeterChange GetNextMeter(MeterChange bpm) => GetNextMeter(bpm.TGrid);

        public MeterChange GetNextMeter(TGrid time) => this.FirstOrDefault(bpm => time < bpm.TGrid);

        private List<(double startY, MeterChange meterChange, BPMChange bpmChange)> cachedBpmUniformPosition = new();
        private double cachedBpmTUnitLength = int.MinValue;

        private void UpdateCachedAllTimeSignatureUniformPositionList(double tUnitLength, BpmList bpmList)
        {
            cachedBpmTUnitLength = tUnitLength;
            cachedBpmUniformPosition.Clear();

            //最初默认的
            cachedBpmUniformPosition.Add((0, FirstMeter, bpmList.FirstBpm));

            var bpmUnitList = bpmList.GetCachedAllBpmUniformPositionList(tUnitLength);

            foreach (var meterChange in changedMeterList)
            {
                (var startY, var refBpm) = bpmUnitList.LastOrDefault(x => x.bpm.TGrid <= meterChange.TGrid);
                var meterY = MathUtils.CalculateBPMLength(refBpm, meterChange.TGrid, tUnitLength);
                cachedBpmUniformPosition.Add((meterY, meterChange, refBpm));
            }

            foreach ((var startY, var bpm) in bpmUnitList.Skip(1))
            {
                var meter = GetMeter(bpm.TGrid);
                cachedBpmUniformPosition.Add((startY, meter, bpm));
            }

            cachedBpmUniformPosition.SortBy(x => x.startY);

            //remove conflict meter position.
            var conflictGroups = cachedBpmUniformPosition.GroupBy(x => x.startY).Where(x => x.Count() > 1);
            //using var disp = ObjectPool<HashSet<(double startY, MeterChange meterChange, BPMChange bpmChange)>>.GetWithUsingDisposable(out var removeSet,out _);
            var removeSet = new HashSet<(double startY, MeterChange meterChange, BPMChange bpmChange)>();
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
                cachedBpmUniformPosition.Remove(item);
        }

        public List<(double startY, MeterChange meter,BPMChange bpm)> GetCachedAllTimeSignatureUniformPositionList(double tUnitLength, BpmList bpmList)
        {
            if (tUnitLength != cachedBpmTUnitLength)
                UpdateCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
            return cachedBpmUniformPosition;
        }
    }
}
