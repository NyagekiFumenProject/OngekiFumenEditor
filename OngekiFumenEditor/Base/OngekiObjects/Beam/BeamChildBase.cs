using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public abstract class BeamChildBase : BeamBase
    {
        public override int RecordId { get => ReferenceBeam.RecordId; set { } }

        public BeamStart ReferenceBeam { get; set; }
        public BeamBase PrevBeam { get; set; }
    }
}
