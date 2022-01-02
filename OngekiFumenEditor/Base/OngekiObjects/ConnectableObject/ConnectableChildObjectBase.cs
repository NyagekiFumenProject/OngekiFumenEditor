using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableChildObjectBase : ConnectableObjectBase
    {
        public override int RecordId { get => ReferenceStartObject.RecordId; set { } }

        public ConnectableStartObject ReferenceStartObject { get; set; }
        public ConnectableObjectBase PrevObject { get; set; }
    }
}
