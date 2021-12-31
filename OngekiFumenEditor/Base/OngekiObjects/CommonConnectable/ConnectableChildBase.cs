using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.CommonConnectable
{
    public abstract class ConnectableChildBase : ConnectableObjectBase
    {
        public override int RecordId { get => ReferenceObject.RecordId; set { } }

        public ConnectableObjectBase ReferenceObject { get; set; }
        public ConnectableObjectBase PrevObject { get; set; }
    }
}
