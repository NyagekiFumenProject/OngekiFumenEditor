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
                case nameof(Soflan.ApplySpeedInDesignMode):
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

        private List<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> cachedSoflanPositionList_DesignMode = new();
        private List<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> cachedSoflanPositionList_PreviewMode = new();
        private double cachedSoflanListCacheHash = int.MinValue;

        [Flags]
        private enum ChgEvt
        {
            None = 0,
            BpmChanged = 1,
            SoflanBegan = 2,
            SoflanEnded = 4,
            SoflanChanged = SoflanBegan | SoflanEnded
        }

        #region SoflanPositionList

        private IEnumerable<(TGrid TGrid, double speed, BPMChange curBpm, ChgEvt)> GetCalculatableEvents(BpmList bpmList, bool isDesignModel)
        {
            var curBpm = bpmList.FirstBpm;
            Soflan curSpeedEvent = null;

            IEnumerable<(TGrid TGrid, double speed, BPMChange curBpm, ChgEvt evt)> GetEventTimings(ITimelineObject evt)
            {
                var t = evt.TGrid;
                switch (evt)
                {
                    case BPMChange bpmEvt:
                        curBpm = bpmEvt;
                        var speed = (curSpeedEvent is not null && curSpeedEvent.EndTGrid > t) ? (isDesignModel ? curSpeedEvent.SpeedInEditor : curSpeedEvent.Speed) : 1.0d;
                        yield return (evt.TGrid, speed, curBpm, ChgEvt.BpmChanged);
                        break;
                    case Soflan soflanEvt:
                        curSpeedEvent = soflanEvt;
                        yield return (evt.TGrid, (isDesignModel ? soflanEvt.SpeedInEditor : soflanEvt.Speed), curBpm, ChgEvt.SoflanBegan);
                        var endTGrid = evt.TGrid + new GridOffset(0, soflanEvt.GridLength);
                        yield return (endTGrid, 1.0f, bpmList.GetBpm(endTGrid), ChgEvt.SoflanEnded);
                        break;
                }
            }
            var r = this.AsEnumerable<ITimelineObject>().Concat(bpmList)
                .OrderBy(x => x.TGrid)
                .SelectMany(GetEventTimings)
                .GroupBy(x => x.TGrid);
                
            return r
                .Select(x =>
                {
                    var itor = x.GetEnumerator();
                    if (itor.MoveNext())
                    {
                        var totalState = itor.Current;
                        while (itor.MoveNext())
                        {
                            var curState = itor.Current;

                            totalState.evt |= curState.evt;
                            switch (curState.evt)
                            {
                                case ChgEvt.SoflanEnded:
                                    if (!totalState.evt.HasFlag(ChgEvt.SoflanBegan))
                                        totalState.speed = curState.speed;
                                    break;
                                case ChgEvt.SoflanBegan:
                                    totalState.speed = curState.speed;
                                    break;
                                case ChgEvt.BpmChanged:
                                    totalState.curBpm = curState.curBpm;
                                    break;
                                default:
                                    break;
                            }
                        }

                        return totalState;
                    }
                    return default;
                })
                .Where(x => x.evt != ChgEvt.None);
        }

        private void UpdateCachedSoflanPositionList(double tUnitLength, BpmList bpmList, List<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> list, bool isDesignMode)
        {
            list.Clear();

            var eventList = GetCalculatableEvents(bpmList, isDesignMode);

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

                //todo 什么时候能够实现倒车Soflan显示在处理吧~
                //var scaledLen = len * (isDesignMode ? Math.Abs(prevEvent.speed) : prevEvent.speed
                var scaledLen = len * Math.Abs(prevEvent.speed);

                var fromY = currentY;
                var toY = currentY + scaledLen;

                list.Add((fromY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));

                currentY = toY;
                prevEvent = curEvent;
            }

            if (list.Count == 0)
                list.Add((0, TGrid.Zero, 1.0d, bpmList.FirstBpm));
            else if (prevEvent.TGrid != list.First().startTGrid)
                list.Add((currentY, prevEvent.TGrid, prevEvent.speed, prevEvent.curBpm));
        }

        private void CheckAndUpdateSoflanPositionList(double tUnitLength, BpmList bpmList)
        {
            var hash = HashCode.Combine(tUnitLength, bpmList.cachedBpmContentHash);

            if (cachedSoflanListCacheHash != hash)
            {
                Log.LogDebug("recalculate all.");
                UpdateCachedSoflanPositionList(tUnitLength, bpmList, cachedSoflanPositionList_DesignMode, true);
                UpdateCachedSoflanPositionList(tUnitLength, bpmList, cachedSoflanPositionList_PreviewMode, false);
                cachedSoflanListCacheHash = hash;
            }
        }

        public IReadOnlyList<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> GetCachedSoflanPositionList_DesignMode(double tUnitLength, BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);
            return cachedSoflanPositionList_DesignMode;
        }

        public IReadOnlyList<(double startY, TGrid startTGrid, double speed, BPMChange bpmChange)> GetCachedSoflanPositionList_PreviewMode(double tUnitLength, BpmList bpmList)
        {
            CheckAndUpdateSoflanPositionList(tUnitLength, bpmList);
            return cachedSoflanPositionList_PreviewMode;
        }

        #endregion
    }
}
