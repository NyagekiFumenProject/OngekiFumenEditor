using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using OngekiFumenEditor.Kernel.CurveInterpolater.OgkrImpl.Enumerator;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.OgkrImpl.Factory
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
