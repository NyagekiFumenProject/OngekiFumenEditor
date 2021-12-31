using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public abstract class WallChildBase : WallBase
    {
        public override int RecordId { get => ReferenceWall.RecordId; set { } }

        public WallStart ReferenceWall { get; set; }
        public WallBase PrevWall { get; set; }
    }
}
