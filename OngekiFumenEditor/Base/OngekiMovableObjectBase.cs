using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiMovableObjectBase : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        private XGrid xGrid = new XGrid();
        public virtual XGrid XGrid
        {
            get { return xGrid; }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(xGrid, value);
                xGrid = value;
                NotifyOfPropertyChange(() => TGrid);
            }
        }

        public override string ToString() => $"{base.ToString()} XGrid:{XGrid}";
    }
}
