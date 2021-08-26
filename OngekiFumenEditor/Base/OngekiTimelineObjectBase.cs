using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiTimelineObjectBase : OngekiObjectBase, ITimelineObject
    {
        private TGrid tGrid = new TGrid();
        public TGrid TGrid
        {
            get { return tGrid; }
            set
            {
                tGrid = value;
                NotifyOfPropertyChange(() => TGrid);
            }
        }

        public int CompareTo(object obj)
        {
            return TGrid.CompareTo((obj as ITimelineObject)?.TGrid);
        }
    }
}
