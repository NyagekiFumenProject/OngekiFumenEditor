using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Utils;
using OpenTK.Mathematics;

namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Enumerator;

public class DefaultCurveInterpolateEnumerator : ICurveInterpolateEnumerator
{
    private readonly LinkedList<CurvePoint> waiter = new();
    private readonly IEnumerator<CurvePoint> itor;

    public DefaultCurveInterpolateEnumerator(ConnectableStartObject start) : this(start.Children.FirstOrDefault(), default)
    {
    }

    public DefaultCurveInterpolateEnumerator(ConnectableChildObjectBase from, ConnectableChildObjectBase to = default)
    {
        var children = from.ReferenceStartObject.Children
            .SkipWhile(x => x != from)
            .TakeWhile(x => x != to)
            .ToArray();

        itor = children
            .SelectMany(Interpolate)
            .DistinctContinuousBy((a, b) => a.TGrid == b.TGrid && a.XGrid == b.XGrid)
            .GetEnumerator();
    }

    protected virtual IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
    {
        CurvePoint Build(Vector2 p)
        {
            var xGrid = new XGrid(p.X / x.XGrid.ResX);
            xGrid.NormalizeSelf();
            var tGrid = new TGrid(p.Y / x.TGrid.ResT);
            tGrid.NormalizeSelf();
            return new CurvePoint(tGrid, xGrid);
        }

        return x.GetConnectionPaths().Select(path => Build(path.pos));
    }

    public void PushBack(CurvePoint point)
    {
        waiter.AddFirst(point);
    }

    public virtual CurvePoint? EnumerateNext()
    {
        if (waiter.Count > 0)
        {
            var d = waiter.First.Value;
            waiter.RemoveFirst();
            return d;
        }

        if (itor.MoveNext())
            return itor.Current;

        return default;
    }
}

