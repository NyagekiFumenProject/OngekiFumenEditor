using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableObjectBase : OngekiMovableObjectBase
    {
        public abstract int RecordId { get; set; }

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {RecordId} {TGrid.Serialize(fumenData)} {XGrid.Serialize(fumenData)}";
        }
    }
}
