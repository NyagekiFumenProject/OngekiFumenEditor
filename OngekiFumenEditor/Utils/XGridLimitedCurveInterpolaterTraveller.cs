using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Utils
{
    public class XGridLimitedCurveInterpolaterTraveller : CurveInterpolaterTraveller
    {
        public XGridLimitedCurveInterpolaterTraveller(ConnectableStartObject start) : base(start)
        {
        }

        public XGridLimitedCurveInterpolaterTraveller(ConnectableChildObjectBase from, ConnectableChildObjectBase to = null) : base(from, to)
        {
        }

        protected override IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
        {
            var itor = base.Interpolate(x).GetEnumerator();
            if (!itor.MoveNext())
                yield break;
            yield return itor.Current;
            var prev = itor.Current;
            var prevRetY = (float)prev.TGrid.TotalGrid / prev.TGrid.ResT;
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

                Log.LogDebug($"--------------");
                Log.LogDebug($"current ({cur})");

                var isZeroSpecial = prevXunitInt == curXunitInt && curXunitInt == 0 && prevXunit * curXunit < 0;

                if (curXunit == curXunitInt)
                {
                    Log.LogDebug($"return ({cur}) directly because curXunitInt == curXunit");
                    prevRetY = curY * 1f / cur.TGrid.ResT;
                    yield return cur;
                }
                else if (prevXunitInt != curXunitInt || isZeroSpecial)
                {
                    Log.LogDebug($"begin interpolate from ({prev}) to ({cur})");

                    foreach (var i in MathUtils.GetIntegersBetweenTwoValues(prevXunit, curXunit))
                    {
                        /*
                     
                                 calculate there between cp1 and cp2
                                  |         |
                                  v         v
                           
               CurvePoint2(prev)  |         |        X[2.5,0]
               -------------o-----|---------|--------o----------------------
                      X[0.5,0]    |         |          CurvePoint2(cur)
                                  |         |
                         xunitLine1 X[1,0]   xunitLine1 X[2,0] 
                    */
                        var xGrid = new XGrid(i, 0, prev.XGrid.ResX);
                        var y = MathUtils.CalculateYFromTwoPointFormFormula(xGrid.TotalGrid, prevX, prevY, curX, curY);
                        var tunit = (float)(y / prev.TGrid.ResT);
                        var tGrid = new TGrid(tunit, 0);
                        Log.LogDebug($"interpolate xunit:{i} from ({prev}) to ({cur})");

                        if (Math.Abs(prevRetY - tunit) > 0.0001)
                        {
                            var point = new CurvePoint()
                            {
                                XGrid = xGrid,
                                TGrid = tGrid,
                            };
                            Log.LogDebug($"return new interpolated point: ({point})");
                            yield return point;
                        }
                        else
                        {
                            Log.LogDebug($"return Math.Abs(prevRetY({prevRetY}) - tunit({tunit})) < 0.01");
                        }
                        prevRetY = tunit;
                    }
                }
                else
                {
                    Log.LogDebug($"return nothing prevXunitInt({prevXunitInt}) == curXunitInt({curXunitInt})");
                }

                prev = cur;
            }
            yield return prev;
        }

        public override CurvePoint? Travel()
        {
            if (base.Travel() is not CurvePoint p)
                return default;

            return new CurvePoint()
            {
                TGrid = p.TGrid,
                XGrid = new XGrid((int)(p.XGrid.TotalGrid * 1.0f / p.XGrid.ResX)),
            };
        }
    }
}
