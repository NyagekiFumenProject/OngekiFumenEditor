using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Hold : OngekiMovableObjectBase, ILaneDockableChangable
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
            }
        }

        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias("RefLaneId")]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

        private int? referenceLaneStrIdManualSet = default;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserTipText("改变此值可以改变此物件对应的轨道所属")]
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

        public void SetHold(HoldEnd end)
        {
            if (holdEnd is not null)
                holdEnd.PropertyChanged -= HoldEnd_PropertyChanged;
            if (end is not null)
                end.PropertyChanged += HoldEnd_PropertyChanged;

            holdEnd = end;

            if (end is not null)
            {
                end.RefHold?.SetHold(null);
                end.RefHold = this;
            }
        }

        private void HoldEnd_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //todo?
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
            yield return HoldEnd;
        }
    }
}
