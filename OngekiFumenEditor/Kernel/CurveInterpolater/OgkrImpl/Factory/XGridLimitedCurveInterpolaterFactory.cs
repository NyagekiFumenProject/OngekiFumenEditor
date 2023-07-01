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
    public class XGridLimitedCurveInterpolaterFactory : ICurveInterpolaterFactory
    {
        public static ICurveInterpolaterFactory Default { get; } = new XGridLimitedCurveInterpolaterFactory();
        public string Name => "XGrid.Unit限制(音击钦定)";

        public ICurveInterpolateEnumerator CreateInterpolaterForAll(ConnectableStartObject start)
        {
            return new XGridLimitedCurveInterpolateEnumerator(start);
        }

        public ICurveInterpolateEnumerator CreateInterpolaterForRange(ConnectableChildObjectBase start, ConnectableChildObjectBase end)
        {
            return new XGridLimitedCurveInterpolateEnumerator(start, end);
        }
    }
}
