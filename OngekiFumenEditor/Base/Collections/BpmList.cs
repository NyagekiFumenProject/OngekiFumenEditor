using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class BpmList : IBinaryFindRangeEnumable<BPMChange, TGrid>
    {
        private BPMChange firstBpm = new BPMChange();
        private TGridSortList<BPMChange> changedBpmList = new();
        public BPMChange FirstBpm => firstBpm;

        public event Action OnChangedEvent;

        public BpmList(IEnumerable<BPMChange> initBpmChanges = default)
        {
            OnChangedEvent += BpmList_OnChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<BPMChange>())
                Add(item);
        }

        private void BpmList_OnChangedEvent()
        {
            cachedBpmContentHash = int.MinValue;
        }

        public void Add(BPMChange bpm)
        {
            changedBpmList.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
            OnChangedEvent?.Invoke();
        }

        public void SetFirstBpm(BPMChange firstBpm)
        {
            this.firstBpm = firstBpm;
            OnChangedEvent?.Invoke();
            firstBpm.PropertyChanged += OnBpmPropChanged;
        }

        public void Remove(BPMChange bpm)
        {
            if (bpm == firstBpm)
                throw new Exception($"BpmList can't delete firstBpm : {bpm}");
            changedBpmList.Remove(bpm);
            bpm.PropertyChanged -= OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        public IEnumerator<BPMChange> GetEnumerator()
        {
            yield return firstBpm;
            foreach (var item in changedBpmList)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public BPMChange GetBpm(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid <= time);

        public BPMChange GetPrevBpm(BPMChange time) => GetPrevBpm(time.TGrid);

        public BPMChange GetPrevBpm(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid < time);

        public BPMChange GetNextBpm(BPMChange bpm) => GetNextBpm(bpm.TGrid);

        public BPMChange GetNextBpm(TGrid time) => this.FirstOrDefault(bpm => time < bpm.TGrid);

        private List<(double startY, BPMChange bpm)> cachedBpmUniformPosition = new();
        internal double cachedBpmContentHash = int.MinValue;

        private void UpdateCachedAllBpmUniformPositionList(double tUnitLength)
        {
            cachedBpmUniformPosition.Clear();

            var prev = FirstBpm;
            var y = 0d;

            cachedBpmUniformPosition.Add((0, FirstBpm));

            while (true)
            {
                var cur = GetNextBpm(prev);
                if (cur is null)
                    break;
                var len = MathUtils.CalculateBPMLength(prev, cur.TGrid, tUnitLength);
                prev = cur;
                y += len;
                cachedBpmUniformPosition.Add((y, cur));
            }
        }

        public List<(double startY, BPMChange bpm)> GetCachedAllBpmUniformPositionList(double tUnitLength)
        {
            int calcHash(BPMChange e) => HashCode.Combine(e.BPM, e.TGrid.Grid, e.TGrid.Unit, e.TGrid.ResT);
            var hash = this.Aggregate(calcHash(FirstBpm), (x, e) => HashCode.Combine(x, calcHash(e)));
            hash = HashCode.Combine(hash, tUnitLength);

            if (hash != cachedBpmContentHash)
            {
                //Log.LogDebug("recalculate all bpm postions.");
                UpdateCachedAllBpmUniformPositionList(tUnitLength);
                cachedBpmContentHash = hash;
            }

            return cachedBpmUniformPosition;
        }

        public (int minIndex, int maxIndex) BinaryFindRangeIndex(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).BinaryFindRangeIndex(min, max);

        public IEnumerable<BPMChange> BinaryFindRange(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).BinaryFindRange(min, max);

        public bool Contains(BPMChange obj)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).Contains(obj);
    }
}
