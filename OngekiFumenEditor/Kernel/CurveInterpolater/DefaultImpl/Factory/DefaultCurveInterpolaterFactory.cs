using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory
{
    [Export(typeof(ICurveInterpolaterFactory))]
    public class DefaultCurveInterpolaterFactory : ICurveInterpolaterFactory
    {
        public static ICurveInterpolaterFactory Default { get; } = new DefaultCurveInterpolaterFactory();

        public string Name => "默认实现";

        public ICurveInterpolateEnumerator CreateInterpolaterForAll(ConnectableStartObject start)
        {
            return new DefaultCurveInterpolateEnumerator(start);
        }

        public ICurveInterpolateEnumerator CreateInterpolaterForRange(ConnectableChildObjectBase start, ConnectableChildObjectBase end)
        {
            return new DefaultCurveInterpolateEnumerator(start, end);
        }
    }
}
