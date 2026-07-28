using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Enumerator;

public class XGridLimitedCurveInterpolateEnumerator : DefaultCurveInterpolateEnumerator
{
    public XGridLimitedCurveInterpolateEnumerator(ConnectableStartObject start) : base(start)
    {
    }

    public XGridLimitedCurveInterpolateEnumerator(ConnectableChildObjectBase from, ConnectableChildObjectBase to = null) : base(from, to)
    {
    }

    private IEnumerable<CurvePoint> InterpolateCore(ConnectableChildObjectBase x)
    {
        using var itor = base.Interpolate(x).GetEnumerator();
        if (!itor.MoveNext())
            yield break;

        yield return itor.Current;
        var prev = itor.Current;
        var prevRetY = (float)prev.TGrid.TotalGrid / prev.TGrid.ResT;
        float? prevAppendNewCornerPointFlag = default;

        while (itor.MoveNext())
        {
            var cur = itor.Current;

            var prevXunit = prev.XGrid.TotalGrid * 1.0f / prev.XGrid.ResX;
            var prevXunitInt = (int)prevXunit;
            var curXunit = cur.XGrid.TotalGrid * 1.0f / cur.XGrid.ResX;
            var curXunitInt = (int)curXunit;
            var prevX = prev.XGrid.TotalGrid;
            var prevY = prev.TGrid.TotalGrid;
            var curX = cur.XGrid.TotalGrid;
            var curY = cur.TGrid.TotalGrid;

            var appendNewCornerPointFlag = Math.Sign(curX - prevX);
            if (prevAppendNewCornerPointFlag is not null)
            {
                if (appendNewCornerPointFlag * prevAppendNewCornerPointFlag < 0)
                {
                    var rawXGridUnit = prev.XGrid.TotalGrid * 1.0 / prev.XGrid.ResX;
                    var judge = rawXGridUnit - (int)rawXGridUnit;
                    if (Math.Abs(judge) > 0.50)
                    {
                        var newXUnit = (int)rawXGridUnit + (judge > 0 ? 1 : -1);
                        yield return new CurvePoint
                        {
                            XGrid = new XGrid(newXUnit, 0),
                            TGrid = prev.TGrid.CopyNew()
                        };
                    }
                }
            }
            prevAppendNewCornerPointFlag = appendNewCornerPointFlag;

            var isZeroSpecial = prevXunitInt == curXunitInt && curXunitInt == 0 && prevXunit * curXunit < 0;

            if (curXunit == curXunitInt)
            {
                prevRetY = curY * 1f / cur.TGrid.ResT;
                yield return cur;
            }
            else if (prevXunitInt != curXunitInt || isZeroSpecial)
            {
                foreach (var i in MathUtils.GetIntegersBetweenTwoValues(prevXunit, curXunit))
                {
                    var xGrid = new XGrid(i, 0);
                    var y = MathUtils.CalculateYFromTwoPointFormFormula(xGrid.TotalGrid, prevX, prevY, curX, curY);
                    var tunit = (float)(y / prev.TGrid.ResT);
                    var tGrid = new TGrid(tunit, 0);

                    if (Math.Abs(prevRetY - tunit) > 0.0001)
                    {
                        yield return new CurvePoint
                        {
                            XGrid = xGrid,
                            TGrid = tGrid,
                        };
                    }
                    prevRetY = tunit;
                }
            }

            prev = cur;
        }

        yield return prev;
    }

    protected override IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
    {
        return InterpolateCore(x);
    }

    public override CurvePoint? EnumerateNext()
    {
        if (base.EnumerateNext() is not CurvePoint p)
            return default;

        return new CurvePoint
        {
            TGrid = p.TGrid,
            XGrid = new XGrid((int)(p.XGrid.TotalGrid * 1.0f / p.XGrid.ResX)),
        };
    }
}

