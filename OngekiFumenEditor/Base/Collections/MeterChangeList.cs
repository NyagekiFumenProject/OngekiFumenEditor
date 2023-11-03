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
			foreach (var item in changedMeterList.OrderBy(x => x.TGrid))
				yield return item;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public MeterChange GetMeter(TGrid time) => this.LastOrDefault(meter => meter.TGrid <= time);

		public MeterChange GetPrevMeter(MeterChange time) => GetPrevMeter(time.TGrid);

		public MeterChange GetPrevMeter(TGrid time) => this.LastOrDefault(meter => meter.TGrid < time);

		public MeterChange GetNextMeter(MeterChange meter) => GetNextMeter(meter.TGrid);

		public MeterChange GetNextMeter(TGrid time) => this.FirstOrDefault(meter => time < meter.TGrid);

		private List<(TimeSpan audioTime, TGrid startTGrid, MeterChange meterChange, BPMChange bpmChange)> cachedTimesignUniformPosition = new();
		private double cachedMetListCacheHash = int.MinValue;

		private void UpdateCachedAllTimeSignatureUniformPositionList(BpmList bpmList)
		{
			TGrid pickBiggerTGrid(ITimelineObject a, ITimelineObject b) => a.TGrid > b.TGrid ? a.TGrid : b.TGrid;

			cachedTimesignUniformPosition.Clear();

			//最初默认的
			cachedTimesignUniformPosition.Add((TimeSpan.FromMilliseconds(0), pickBiggerTGrid(FirstMeter, bpmList.FirstBpm), FirstMeter, bpmList.FirstBpm));

			var bpmUnitList = bpmList.GetCachedAllBpmUniformPositionList();

			foreach (var meterChange in changedMeterList)
			{
				(var audioTime, var refBpm) = bpmUnitList.LastOrDefault(x => x.bpm.TGrid <= meterChange.TGrid);
				var meterY = audioTime + TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(refBpm, meterChange.TGrid));
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
