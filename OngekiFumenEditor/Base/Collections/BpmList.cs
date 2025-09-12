using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Base.Collections
{
    public class BpmList : IBinaryFindRangeEnumable<BPMChange, TGrid>
    {
        public const double DefaultFirstBpm = 240;

        private TGridSortList<BPMChange> changedBpmList = new();

        public int Count => 1 + changedBpmList.Count;

        public event Action OnChangedEvent;

        public double FirstBpm
        {
            get
            {
                return changedBpmList.FirstOrDefault()?.BPM ?? DefaultFirstBpm;
            }
            set
            {
                if (changedBpmList.FirstOrDefault() is not BPMChange bpmChange)
                {
                    bpmChange = new BPMChange()
                    {
                        TGrid = new(0, 0)
                    };
                    Add(bpmChange);
                }

                bpmChange.BPM = value;
            }
        }

        public BpmList(IEnumerable<BPMChange> initBpmChanges = default)
        {
            FirstBpm = DefaultFirstBpm;

            OnChangedEvent += BpmList_OnChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<BPMChange>())
                Add(item);
        }

        private void BpmList_OnChangedEvent()
        {
            cachedBpmContentHash = RandomHepler.Random(int.MinValue, int.MaxValue);
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

        public bool Remove(BPMChange bpm)
        {
            if (bpm == changedBpmList.FirstOrDefault())
                throw new Exception($"BpmList can't delete firstBpm : {bpm}, but you can use SetFirstBpm({bpm})");
            var r = changedBpmList.Remove(bpm);
            if (r)
            {
                bpm.PropertyChanged -= OnBpmPropChanged;
                OnChangedEvent?.Invoke();
            }
            return r;
        }

        public IEnumerator<BPMChange> GetEnumerator()
        {
            return changedBpmList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBpmIndex(TGrid time)
        {
            var idx = changedBpmList.BinarySearchBy(time);
            var actualIdx = idx < 0 ? ((~idx) - 1) : idx;
            return actualIdx;
        }

        public BPMChange GetBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList[idx];
        }

        public BPMChange GetPrevBpm(BPMChange time) => GetPrevBpm(time.TGrid);

        public BPMChange GetPrevBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList.ElementAtOrDefault(idx - 1);
        }

        public BPMChange GetNextBpm(BPMChange bpm) => GetNextBpm(bpm.TGrid);

        public BPMChange GetNextBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList.ElementAtOrDefault(idx + 1);
        }

        private List<(TimeSpan audioTime, BPMChange bpm)> cachedBpmUniformPosition = new();
        internal int cachedBpmContentHash = RandomHepler.Random(int.MinValue, int.MaxValue);

        private void UpdateCachedAllBpmUniformPositionList()
        {
            cachedBpmUniformPosition.Clear();

            var itor = changedBpmList.GetEnumerator();

            if (itor.MoveNext())
            {
                var prev = itor.Current;
                var currentTimeMs = 0d;

                cachedBpmUniformPosition.Add((TimeSpan.FromMilliseconds(0), prev));

                while (itor.MoveNext())
                {
                    var cur = itor.Current;
                    if (cur is null)
                        break;
                    var len = MathUtils.CalculateBPMLength(prev, cur.TGrid);
                    prev = cur;
                    currentTimeMs += len;

                    var time = TimeSpan.FromMilliseconds(currentTimeMs);
                    cachedBpmUniformPosition.Add((time, cur));
                }
            }
        }

        public List<(TimeSpan audioTime, BPMChange bpm)> GetCachedAllBpmUniformPositionList()
        {
            int calcHash(BPMChange e) => HashCode.Combine(e.BPM, e.TGrid.TotalGrid);
            var hash = this.Aggregate(0, (x, e) => HashCode.Combine(x, calcHash(e)));
            hash = HashCode.Combine(hash);

            if (hash != cachedBpmContentHash)
            {
                //Log.LogDebug("recalculate all bpm postions.");
                UpdateCachedAllBpmUniformPositionList();
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
