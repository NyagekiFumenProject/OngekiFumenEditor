using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor;
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
