using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Tap : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        private XGrid xGrid = new XGrid();
        public XGrid XGrid
        {
            get
            {
                return xGrid;
            }
            set
            {
                xGrid = value;
                NotifyOfPropertyChange(() => XGrid);
            }
        }

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
            }
        }

        public override Type ModelViewType => throw new NotImplementedException();

        public override string IDShortName => IsCritical ? "CTP" : "TAP";

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {ReferenceLaneStart.RecordId} {TGrid.Serialize(fumenData)} {XGrid.Unit} {XGrid.Grid}";
        }
    }
}
