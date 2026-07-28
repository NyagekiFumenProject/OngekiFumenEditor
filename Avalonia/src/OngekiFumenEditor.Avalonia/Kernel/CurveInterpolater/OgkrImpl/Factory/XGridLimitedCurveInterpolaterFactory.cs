using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Enumerator;

namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;

[RegisterSingleton<ICurveInterpolaterFactory>]
public class XGridLimitedCurveInterpolaterFactory : ICurveInterpolaterFactory
{
    public static ICurveInterpolaterFactory Default { get; } = new XGridLimitedCurveInterpolaterFactory();
    public string Name => "XGrid.Unit limited";

    public ICurveInterpolateEnumerator CreateInterpolaterForAll(ConnectableStartObject start)
    {
        return new XGridLimitedCurveInterpolateEnumerator(start);
    }

    public ICurveInterpolateEnumerator CreateInterpolaterForRange(ConnectableChildObjectBase start, ConnectableChildObjectBase end)
    {
        return new XGridLimitedCurveInterpolateEnumerator(start, end);
    }
}

