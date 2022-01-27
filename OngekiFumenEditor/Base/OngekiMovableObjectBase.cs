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
                NotifyOfPropertyChange(() => XGrid);
            }
        }

        public override string ToString() => $"{base.ToString()} XGrid:{XGrid}";

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not OngekiMovableObjectBase from)
                return;

            XGrid = from.XGrid;
        }
    }
}
