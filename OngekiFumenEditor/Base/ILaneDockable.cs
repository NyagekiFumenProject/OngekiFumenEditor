using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public interface ILaneDockable
    {
        LaneStartBase ReferenceLaneStart { get; set; }
    }
}
