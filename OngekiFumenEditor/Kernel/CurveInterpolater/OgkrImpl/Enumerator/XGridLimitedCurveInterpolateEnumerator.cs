using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.OgkrImpl.Enumerator
{
    public class XGridLimitedCurveInterpolateEnumerator : DefaultCurveInterpolateEnumerator
    {
        public XGridLimitedCurveInterpolateEnumerator(ConnectableStartObject start) : base(start)
        {
        }

        public XGridLimitedCurveInterpolateEnumerator(ConnectableChildObjectBase from, ConnectableChildObjectBase to = null) : base(from, to)
        {
        }

        private static IEnumerable<int> GetIntegersBetweenTwoValues(double from, double to)
        {
            var sign = Math.Sign(to - from);
            var begin = 0;
            var end = 0;

            if (sign > 0)
            {
                begin = (int)Math.Ceiling(from);
                end = (int)Math.Floor(to);
            }

            if (sign < 0)
            {
                begin = (int)Math.Floor(from);
                end = (int)Math.Ceiling(to);
            }

            for (var i = begin; sign > 0 ? i <= end : i >= end; i += sign)
                yield return i;
        }

        private static double CalculateYFromTwoPointFormFormula(double x, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return y1;

            return (x - x1) / bx * by + y1;
        }

        private IEnumerable<CurvePoint> InterpolateCore(ConnectableChildObjectBase x)
        {
            using var itor = base.Interpolate(x).GetEnumerator();
            if (!itor.MoveNext())
                yield break;
            yield return itor.Current;
            var prev = itor.Current;
            var prevRetY = (float)prev.TGrid.TotalGrid / prev.TGrid.ResT;
            var prevAppendNewCornerPointFlag = default(float?);

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
                            var newPoint = new CurvePoint
                            {
                                XGrid = new XGrid(newXUnit, 0),
                                TGrid = prev.TGrid.CopyNew()
                            };
                            yield return newPoint;
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
                    foreach (var i in GetIntegersBetweenTwoValues(prevXunit, curXunit))
                    {
                        var xGrid = new XGrid(i, 0);
                        var y = CalculateYFromTwoPointFormFormula(xGrid.TotalGrid, prevX, prevY, curX, curY);
                        var tunit = (float)(y / prev.TGrid.ResT);
                        var tGrid = new TGrid(tunit, 0);

                        if (Math.Abs(prevRetY - tunit) > 0.0001)
                        {
                            var point = new CurvePoint
                            {
                                XGrid = xGrid,
                                TGrid = tGrid,
                            };
                            yield return point;
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
}
