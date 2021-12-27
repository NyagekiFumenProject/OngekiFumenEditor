using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public abstract class BeamBase : OngekiTimelineObjectBase, IHorizonPositionObject, IDisplayableObject
    {
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

        public int WidthId { get; set; }

        public abstract Type ModelViewType { get; }

        public override string Serialize(OngekiFumen fumenData)
        {
            throw new NotImplementedException();
        }
    }
}
