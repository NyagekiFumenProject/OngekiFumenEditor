using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;

namespace OngekiFumenEditor.Kernel.CurveInterpolater
{
	public interface ICurveInterpolaterFactory
	{
		string Name { get; }

		ICurveInterpolateEnumerator CreateInterpolaterForAll(ConnectableStartObject start);
		ICurveInterpolateEnumerator CreateInterpolaterForRange(ConnectableChildObjectBase start, ConnectableChildObjectBase end);
	}
}
