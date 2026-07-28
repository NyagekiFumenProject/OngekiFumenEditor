using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;

public struct CurvePoint
{
    public CurvePoint(TGrid t, XGrid x)
    {
        TGrid = t;
        XGrid = x;
    }

    public TGrid TGrid { get; set; }
    public XGrid XGrid { get; set; }

    public int CompareTo(ITimelineObject obj)
    {
        return TGrid.CompareTo(obj.TGrid);
    }

    public static explicit operator CurvePoint(OngekiMovableObjectBase e)
    {
        return new CurvePoint(e.TGrid, e.XGrid);
    }

    public override readonly string ToString() => $"{XGrid} {TGrid}";
}

