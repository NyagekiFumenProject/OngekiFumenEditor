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
    }
}
