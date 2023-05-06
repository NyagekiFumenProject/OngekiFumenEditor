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
    public class SoflanList : TGridSortList<Soflan>
    {
        public event Action OnChangedEvent;

        public SoflanList(IEnumerable<Soflan> initBpmChanges = default)
        {
            OnChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<Soflan>())
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent()
        {
            cachedSoflanListCacheHash = int.MinValue;
        }

        public override void Add(Soflan bpm)
        {
            base.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Soflan.Speed):
                case nameof(Soflan.TGrid):
                case nameof(Soflan.GridLength):
                    OnChangedEvent?.Invoke();
                    break;
                default:
                    break;
            }
        }

        public override void Remove(Soflan bpm)
        {
            base.Remove(bpm);
            bpm.PropertyChanged -= OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private List<(double startY, TGrid startTGrid, Soflan soflan, BPMChange bpmChange)> cachedSoflanPositionList = new();
        private double cachedSoflanListCacheHash = int.MinValue;

        private void UpdateCachedSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            cachedSoflanPositionList.Clear();

            var curBpm = bpmList.FirstBpm;
            var curSpeed = Soflan.Default;

            var eventList = this.AsEnumerable<ITimelineObject>().Concat(bpmList).OrderBy(x => x.TGrid).Select((evt) =>
            {
                switch (evt)
                {
                    case BPMChange bpmEvt:
                        curBpm = bpmEvt;
                        break;
                    case Soflan soflanEvt:
                        curSpeed = soflanEvt;
                        break;
                    default:
                        break;
                }
                return (evt.TGrid, curSpeed, curBpm);
            }).GroupBy(x => x.TGrid)
            .Select(x => x.LastOrDefault()).ToList();

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

                var len = MathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.curBpm.BPM, tUnitLength);
                var scaledLen = len * prevEvent.curSpeed.Speed;

                var fromY = currentY;
                var toY = currentY + scaledLen;

                cachedSoflanPositionList.Add((fromY, prevEvent.TGrid, prevEvent.curSpeed, prevEvent.curBpm));

                currentY = toY;
                prevEvent = curEvent;
            }

            if (cachedSoflanPositionList.Count == 0)
                cachedSoflanPositionList.Add((0, TGrid.Zero, Soflan.Default, bpmList.FirstBpm));
            else if (prevEvent.TGrid != cachedSoflanPositionList.First().startTGrid)
                cachedSoflanPositionList.Add((currentY, prevEvent.TGrid, prevEvent.curSpeed, prevEvent.curBpm));
        }

        public List<(double startY, TGrid startTGrid, Soflan soflan, BPMChange bpmChange)> GetCachedSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            var hash = HashCode.Combine(tUnitLength, bpmList.cachedBpmContentHash);

            if (cachedSoflanListCacheHash != hash)
            {
                Log.LogDebug("recalculate all.");
                UpdateCachedSoflanPositionList(tUnitLength, bpmList);
                cachedSoflanListCacheHash = hash;
            }
            return cachedSoflanPositionList;
        }
    }
}
