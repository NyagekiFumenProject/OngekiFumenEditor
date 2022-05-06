using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.CurveInterpolater
{
    public interface ICurveInterpolaterFactory
    {
        string Name { get; }

        ICurveInterpolateEnumerator CreateInterpolaterForAll(ConnectableStartObject start);
        ICurveInterpolateEnumerator CreateInterpolaterForRange(ConnectableChildObjectBase start, ConnectableChildObjectBase end);
    }
}
