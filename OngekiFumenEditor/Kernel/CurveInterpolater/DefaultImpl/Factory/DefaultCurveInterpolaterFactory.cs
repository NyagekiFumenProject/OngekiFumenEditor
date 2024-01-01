using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory
{
	[Export(typeof(ICurveInterpolaterFactory))]
	public class DefaultCurveInterpolaterFactory : ICurveInterpolaterFactory
	{
		public static ICurveInterpolaterFactory Default { get; } = new DefaultCurveInterpolaterFactory();

		public string Name => Resources.CurveInterpolaterFactoryDefaultImpl;

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
