using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
	public class BpmList : IBinaryFindRangeEnumable<BPMChange, TGrid>
	{
		private BPMChange firstBpm;
		private TGridSortList<BPMChange> changedBpmList = new();
		public BPMChange FirstBpm => firstBpm;

		public int Count => 1 + changedBpmList.Count;

		public event Action OnChangedEvent;

		public BpmList(IEnumerable<BPMChange> initBpmChanges = default)
		{
			SetFirstBpm(new BPMChange());

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

		public void SetFirstBpm(BPMChange firstBpm)
		{
			if (this.firstBpm != null)
				this.firstBpm.PropertyChanged -= OnBpmPropChanged;
			this.firstBpm = firstBpm;
			OnChangedEvent?.Invoke();
			firstBpm.PropertyChanged += OnBpmPropChanged;
		}

		public bool Remove(BPMChange bpm)
		{
			if (bpm == firstBpm)
				throw new Exception($"BpmList can't delete firstBpm : {bpm}, but you can use SetFirstBpm()");
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

		private List<(TimeSpan audioTime, BPMChange bpm)> cachedBpmUniformPosition = new();
		internal int cachedBpmContentHash = RandomHepler.Random(int.MinValue, int.MaxValue);

		private void UpdateCachedAllBpmUniformPositionList()
		{
			cachedBpmUniformPosition.Clear();

			var prev = FirstBpm;
			var currentTimeMs = 0d;

			cachedBpmUniformPosition.Add((TimeSpan.FromMilliseconds(0), FirstBpm));

			while (true)
			{
				var cur = GetNextBpm(prev);
				if (cur is null)
					break;
				var len = MathUtils.CalculateBPMLength(prev, cur.TGrid);
				prev = cur;
				currentTimeMs += len;

				var time = TimeSpan.FromMilliseconds(currentTimeMs);
				cachedBpmUniformPosition.Add((time, cur));
			}
		}

		public List<(TimeSpan audioTime, BPMChange bpm)> GetCachedAllBpmUniformPositionList()
		{
			int calcHash(BPMChange e) => HashCode.Combine(e.BPM, e.TGrid.TotalGrid);
			var hash = this.Aggregate(calcHash(FirstBpm), (x, e) => HashCode.Combine(x, calcHash(e)));
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
