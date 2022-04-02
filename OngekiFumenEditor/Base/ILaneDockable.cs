using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public interface ILaneDockable : IHorizonPositionObject, ITimelineObject
    {
        LaneStartBase ReferenceLaneStart { get; set; }
        public int ReferenceLaneStrId { get; set; }
    }
}
