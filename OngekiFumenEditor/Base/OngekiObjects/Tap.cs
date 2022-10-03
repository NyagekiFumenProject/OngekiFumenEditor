using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Tap : OngekiMovableObjectBase, ILaneDockable
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

        private int referenceLaneStrId = default;
        public int ReferenceLaneStrId
        {
            get { return ReferenceLaneStart?.RecordId ?? referenceLaneStrId; }
            set
            {
                if (ReferenceLaneStart is null)
                    referenceLaneStrId = value;
                NotifyOfPropertyChange(() => ReferenceLaneStrId);
            }
        }

        public override string IDShortName => IsCritical ? "CTP" : "TAP";

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not Tap from)
                return;

            IsCritical = from.IsCritical;
            ReferenceLaneStart = from.ReferenceLaneStart;
        }
    }
}
