using OngekiFumenEditor.Base.Attributes;
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
    }
}
