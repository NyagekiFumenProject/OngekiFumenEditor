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

        public override void Add(Soflan soflan)
        {
            base.Add(soflan);
            soflan.PropertyChanged += OnSoflanPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnSoflanPropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Soflan.Speed):
                case nameof(Soflan.TGrid):
                case nameof(TGrid.Grid):
                case nameof(TGrid.Unit):
                case nameof(Soflan.EndTGrid):
                case nameof(Soflan.GridLength):
                    OnChangedEvent?.Invoke();
                    break;
                default:
                    break;
            }
        }

        public override void Remove(Soflan soflan)
        {
            base.Remove(soflan);
            soflan.PropertyChanged -= OnSoflanPropChanged;
            OnChangedEvent?.Invoke();
        }

        private List<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> cachedNonNegativeSoflanPositionList = new();
        private double cachedSoflanListCacheHash = int.MinValue;

        private void UpdateCachedSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            cachedNonNegativeSoflanPositionList.Clear();

            var curBpm = bpmList.FirstBpm;
            Soflan curSpeedEvent = null;

            var i = 0;

            IEnumerable<(int id, TGrid TGrid, double speed, BPMChange curBpm)> GetEventTimings(ITimelineObject evt)
            {
                var t = evt.TGrid;
                switch (evt)
                {
                    case BPMChange bpmEvt:
                        curBpm = bpmEvt;
                        var speed = (curSpeedEvent is not null && curSpeedEvent.EndTGrid > t) ? curSpeedEvent.Speed : 1.0d;
                        yield return (i++, evt.TGrid, speed, curBpm);
                        break;
                    case Soflan soflanEvt:
                        curSpeedEvent = soflanEvt;
                        yield return (i++, evt.TGrid, soflanEvt.Speed, curBpm);
                        yield return (i++, evt.TGrid + new GridOffset(0, soflanEvt.GridLength), 1.0f, curBpm);
                        break;
                }
            }
            var eventList = this.AsEnumerable<ITimelineObject>().Concat(bpmList)
                .OrderBy(x => x.TGrid)
                .SelectMany(GetEventTimings)
                .GroupBy(x => x.Item2)
                .Select(x => x.OrderBy(x => x.id).LastOrDefault()).ToList();

            var itor = eventList.GetEnumerator();
            if (!itor.MoveNext())
                return; //不应该出现这种情况的
            var currentY = 0d;

            var prevEvent = itor.Current;

            while (itor.MoveNext())
            {
                /* |------------|--------------|
                  prev                        cur
                    
                 */
                var curEvent = itor.Current;

                var len = MathUtils.CalculateBPMLength(prevEvent.TGrid, curEvent.TGrid, prevEvent.curBpm.BPM, tUnitLength);
                var scaledLen = len * Math.Abs(prevEvent.speed);

                var fromY = currentY;
                var toY = currentY + scaledLen;

                cachedNonNegativeSoflanPositionList.Add((fromY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));

                currentY = toY;
                prevEvent = curEvent;
            }

            if (cachedNonNegativeSoflanPositionList.Count == 0)
                cachedNonNegativeSoflanPositionList.Add((0, TGrid.Zero, 1.0d, bpmList.FirstBpm));
            else if (prevEvent.TGrid != cachedNonNegativeSoflanPositionList.First().startTGrid)
                cachedNonNegativeSoflanPositionList.Add((currentY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));
        }

        public List<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> GetCachedNonNegativeSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            var hash = HashCode.Combine(tUnitLength, bpmList.cachedBpmContentHash);

            if (cachedSoflanListCacheHash != hash)
            {
                Log.LogDebug("recalculate all.");
                UpdateCachedSoflanPositionList(tUnitLength, bpmList);
                cachedSoflanListCacheHash = hash;
            }
            return cachedNonNegativeSoflanPositionList;
        }
    }
}
