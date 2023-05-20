using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.EditorObjects;
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
    public class Hold : ConnectableStartObject, ILaneDockableChangable
    {
        private bool isCritical = false;
        public bool IsCritical
        {
            get { return isCritical; }
            set
            {
                isCritical = value;
                NotifyOfPropertyChange(() => IDShortName);
                NotifyOfPropertyChange(() => IsCritical);
                Children.ForEach(x => x.NotifyOfPropertyChange(() => IsCritical));
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
                Children?.FirstOrDefault()?.NotifyOfPropertyChange(() => ReferenceLaneStart);
            }
        }

        [ObjectPropertyBrowserReadOnly]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;


        private int referenceLaneStrIdManualSet = default;
        [ObjectPropertyBrowserTipText("改变此值可以改变此物件对应的轨道所属")]
        [ObjectPropertyBrowserAlias("手动改变所属轨道Id")]
        public int ReferenceLaneStrIdManualSet
        {
            get => referenceLaneStrIdManualSet;
            set
            {
                referenceLaneStrIdManualSet = value;
                NotifyOfPropertyChange(() => ReferenceLaneStrIdManualSet);
                referenceLaneStrIdManualSet = default;
            }
        }

        public HoldEnd HoldEnd => Children.LastOrDefault() as HoldEnd;


        public override string IDShortName => IsCritical ? "CHD" : "HLD";

        public override Type NextType => null;
        public override Type EndType => typeof(HoldEnd);
    }
}
