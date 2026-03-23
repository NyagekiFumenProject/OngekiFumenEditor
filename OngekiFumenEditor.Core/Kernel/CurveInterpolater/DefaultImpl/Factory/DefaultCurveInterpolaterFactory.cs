using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Kernel.CurveInterpolater.DefaultImpl.Factory
{
	[Export(typeof(ICurveInterpolaterFactory))]
	public class DefaultCurveInterpolaterFactory : ICurveInterpolaterFactory
	{
		public static ICurveInterpolaterFactory Default { get; } = new DefaultCurveInterpolaterFactory();

		public string Name => "Default";

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
