using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableChildObjectBase : ConnectableObjectBase
    {
        public override int RecordId { get => ReferenceStartObject?.RecordId ?? int.MinValue; set { } }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return base.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) || (TGrid > maxVisibleTGrid && PrevObject is not null && PrevObject.TGrid < minVisibleTGrid);
        }

        public ConnectableStartObject ReferenceStartObject { get; set; }
        public ConnectableObjectBase PrevObject { get; set; }

        public override string ToString() => $"{base.ToString()} {RecordId} Ref:{ReferenceStartObject}";
    }
}
