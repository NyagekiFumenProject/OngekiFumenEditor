using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public abstract class BeamBase : OngekiTimelineObjectBase, IHorizonPositionObject, IDisplayableObject
    {
        private int recordId = -1;
        public int RecordId
        {
            get { return recordId; }
            set
            {
                recordId = value;
                NotifyOfPropertyChange(() => RecordId);
            }
        }

        private XGrid xGrid = new XGrid();
        public XGrid XGrid
        {
            get { return xGrid; }
            set
            {
                xGrid = value;
                NotifyOfPropertyChange(() => XGrid);
            }
        }

        public int WidthId { get; set; } = 2;

        public abstract Type ModelViewType { get; }

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {RecordId} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)} {WidthId}";
        }
    }
}
