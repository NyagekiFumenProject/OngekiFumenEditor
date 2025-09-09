using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Hold : OngekiMovableObjectBase, ILaneDockableChangable, ICriticalableObject
    {
        private HoldEnd holdEnd;

        public bool IsWallHold => ReferenceLaneStart?.IsWallLane ?? false;

        private bool isCritical = false;
        public bool IsCritical
        {
            get { return isCritical; }
            set
            {
                isCritical = value;
                NotifyOfPropertyChange(() => IDShortName);
                NotifyOfPropertyChange(() => IsCritical);
            }
        }

        private LaneStartBase referenceLaneStart = default;
        public LaneStartBase ReferenceLaneStart
        {
            get { return referenceLaneStart; }
            set
            {
                referenceLaneStart = value;
                NotifyOfPropertyChange(() => ReferenceLaneStart);
                NotifyOfPropertyChange(() => ReferenceLaneStrId);

                HoldEnd?.RedockXGrid();
            }
        }

        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias("RefLaneId")]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

        private int? referenceLaneStrIdManualSet = default;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserTipText("ObjectLaneGroupId")]
        [ObjectPropertyBrowserAlias("SetRefLaneId")]
        public int? ReferenceLaneStrIdManualSet
        {
            get => referenceLaneStrIdManualSet;
            set
            {
                referenceLaneStrIdManualSet = value;
                NotifyOfPropertyChange(() => ReferenceLaneStrIdManualSet);
                referenceLaneStrIdManualSet = default;
            }
        }

        public HoldEnd HoldEnd => holdEnd;

        public TGrid EndTGrid => HoldEnd?.TGrid ?? TGrid;

        public override string IDShortName => IsCritical ? "CHD" : "HLD";

        public void SetHoldEnd(HoldEnd end)
        {
            if (holdEnd is not null)
                holdEnd.PropertyChanged -= HoldEnd_PropertyChanged;
            if (end is not null)
                end.PropertyChanged += HoldEnd_PropertyChanged;

            holdEnd = end;

            if (end is not null)
            {
                end.RefHold?.SetHoldEnd(null);
                end.RefHold = this;

                end.RedockXGrid();
            }
        }

        private void HoldEnd_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HoldEnd.TGrid):
                    NotifyOfPropertyChange(nameof(EndTGrid));
                    break;
                default:
                    break;
            }
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
            yield return HoldEnd;
        }

        public void CopyEntire(Hold from)
        {
            //包括End一起复制了
            Copy(from);

            if (from.HoldEnd != null)
            {
                //create
                if (holdEnd is null)
                    SetHoldEnd(new HoldEnd());
                holdEnd.Copy(from.HoldEnd);
            }
            else
            {
                //delete
                SetHoldEnd(default);
            }
        }

        public IEnumerable<TGrid> CalculateJudgeTGrid(BpmList bpmList, float progressJudgeBpm)
        {
            return CalculateJudgeTGrid(TGrid, EndTGrid, bpmList, progressJudgeBpm);
        }

        public IEnumerable<TGrid> CalculateJudgeTGrid(TGrid minTGrid, TGrid maxTGrid, BpmList bpmList, float progressJudgeBpm)
        {
            int CalcHoldTickStepSize(double bpm)
            {
                var standardBeatLen = TGrid.DEFAULT_RES_T / 4;

                if (bpm < progressJudgeBpm)
                {
                    var ratio = progressJudgeBpm / bpm;
                    var power = (int)Math.Ceiling(Math.Log2(ratio));
                    standardBeatLen >>= power;
                }
                else
                {
                    var ratio = bpm / progressJudgeBpm;
                    var power = (int)Math.Floor(Math.Log2(ratio));
                    standardBeatLen <<= power;
                }

                return (int)standardBeatLen;
            }

            var holdStartTGrid = TGrid;
            var holdEndTGrid = HoldEnd?.TGrid;
            if (holdEndTGrid is null)
                yield break;

            var curTGrid = holdStartTGrid;

            while (curTGrid < holdEndTGrid)
            {
                var bpm = bpmList.GetBpm(curTGrid);
                var nextTGrid = bpmList.GetNextBpm(curTGrid)?.TGrid ?? TGrid.MaxValue;

                //minTGrid is between this bpm and the next, so we could start to enumerate them from this bpm
                if (bpm.TGrid <= minTGrid && minTGrid <= nextTGrid)
                {
                    var tickGrid = CalcHoldTickStepSize(bpm.BPM);
                    curTGrid = curTGrid + new GridOffset(0, tickGrid);

                    //skip to minTGrid
                    while (curTGrid < minTGrid)
                    {
                        tickGrid = CalcHoldTickStepSize(bpm.BPM);
                        curTGrid = curTGrid + new GridOffset(0, tickGrid);
                    }

                    //enumerate until hold end or maxTGrid
                    while (curTGrid < holdEndTGrid && curTGrid < maxTGrid)
                    {
                        yield return curTGrid;

                        bpm = bpmList.GetBpm(curTGrid);
                        tickGrid = CalcHoldTickStepSize(bpm.BPM);
                        curTGrid = curTGrid + new GridOffset(0, tickGrid);
                    }

                    //finally check if need to yield hold end
                    if (maxTGrid >= holdEndTGrid && curTGrid >= holdEndTGrid)
                        yield return holdEndTGrid;

                    //done :D
                    break;
                }
                else
                {
                    //not in range yet, skip curTGrid to relative pos of next bpm
                    var tickGrid = CalcHoldTickStepSize(bpm.BPM);
                    var nextBpmCurTGrid = curTGrid + new GridOffset(0, tickGrid * ((nextTGrid.TotalGrid - curTGrid.TotalGrid) / tickGrid + 1));

                    curTGrid = nextBpmCurTGrid;
                }
            }
        }
    }
}
