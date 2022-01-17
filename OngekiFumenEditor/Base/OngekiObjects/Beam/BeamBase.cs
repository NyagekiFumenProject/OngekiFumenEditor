using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public abstract class BeamBase : OngekiMovableObjectBase
    {
        public abstract int RecordId { get; set; }

        public int WidthId { get; set; } = 2;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {RecordId} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)} {WidthId}";
        }
    }
}
