using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane.Base
{
    public abstract class LaneStartBase : ConnectableStartObject
    {
        public abstract LaneType LaneType { get; }
        public bool IsWallLane => LaneType == LaneType.WallLeft || LaneType.WallRight == LaneType;
    }
}
