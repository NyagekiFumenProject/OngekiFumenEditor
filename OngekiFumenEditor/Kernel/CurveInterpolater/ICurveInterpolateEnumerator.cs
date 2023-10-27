namespace OngekiFumenEditor.Kernel.CurveInterpolater
{
	public interface ICurveInterpolateEnumerator
	{
		CurvePoint? EnumerateNext();
		void PushBack(CurvePoint point);
	}
}
