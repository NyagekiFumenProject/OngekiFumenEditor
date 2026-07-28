namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;

public interface ICurveInterpolateEnumerator
{
    CurvePoint? EnumerateNext();
    void PushBack(CurvePoint point);
}

