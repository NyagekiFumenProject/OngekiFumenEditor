using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Enumerator;

namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;

[RegisterSingleton<ICurveInterpolaterFactory>]
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

