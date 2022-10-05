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
    public class Hold : ConnectableStartObject, ILaneDockable
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
                ReferenceLaneStrId = value?.RecordId ?? -1;
                Children?.FirstOrDefault()?.NotifyOfPropertyChange(() => ReferenceLaneStart);
            }
        }

        private int referenceLaneStrId = -1;
        [ObjectPropertyBrowserTipText("当前所属轨道物件ID,改变此值可以改变此物件对应的轨道所属")]
        public int ReferenceLaneStrId
        {
            get { return referenceLaneStrId; }
            set
            {
                Set(ref referenceLaneStrId, value);
            }
        }

        public HoldEnd HoldEnd => Children.LastOrDefault() as HoldEnd;


        public override string IDShortName => IsCritical ? "CHD" : "HLD";

        public override Type NextType => null;
        public override Type EndType => typeof(HoldEnd);
    }
}
