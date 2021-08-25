using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiTimelineObjectBase : OngekiObjectBase , ITimelineObject
    {
        public TGrid TGrid { get; set; } = new TGrid();

        public int CompareTo(object obj)
        {
            return TGrid.CompareTo((obj as ITimelineObject)?.TGrid);
        }
    }
}
