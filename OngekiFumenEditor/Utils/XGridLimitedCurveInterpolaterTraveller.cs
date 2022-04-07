using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public class XGridLimitedCurveInterpolaterTraveller : CurveInterpolaterTraveller
    {
        public XGridLimitedCurveInterpolaterTraveller(ConnectableStartObject start) : base(start)
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
                var prevXunitInt = (int)(prev.XGrid.TotalGrid * 1.0f / prev.XGrid.ResX);
                var curXunit = cur.XGrid.TotalGrid * 1.0f / cur.XGrid.ResX;
                var curXunitInt = (int)curXunit;
                var prevX = prev.XGrid.TotalGrid;
                var prevY = prev.TGrid.TotalGrid;
                var curX = cur.XGrid.TotalGrid;
                var curY = cur.TGrid.TotalGrid;

                Log.LogDebug($"--------------");
                Log.LogDebug($"current ({cur})");

                if (curXunit == curXunitInt)
                {
                    Log.LogDebug($"return ({cur}) directly because curXunitInt == curXunit");
                    prevRetY = curY * 1f / cur.TGrid.ResT;
                    yield return cur;
                }
                else if (prevXunitInt != curXunitInt)
                {
                    var signStep = Math.Sign(curXunitInt - prevXunitInt);
                    Log.LogDebug($"begin interpolate from ({prev}) to ({cur})");

                    for (int i = prevXunitInt + 1; signStep > 0 ? i < curXunit : i > curXunit; i += signStep)
                    {
                        /*
                     
                                 calculate there between cp1 and cp2
                                  |         |
                                  v         v
                           
               CurvePoint2(prev)  |         |        X[3.5,0]
               -------------o-----|---------|--------o----------------------
                      X[0.5,0]    |         |          CurvePoint2(cur)
                                  |         |
                         xunitLine1 X[1,0]   xunitLine1 X[2,0] 
                    */


                        var ix = new XGrid(i, 0, prev.XGrid.ResX).TotalGrid;
                        var y = MathUtils.CalculateYFromTwoPointFormFormula(ix, prevX, prevY, curX, curY);
                        var tunit = (float)(y / prev.TGrid.ResT);
                        var tGrid = new TGrid(tunit, 0);
                        Log.LogDebug($"interpolate xunit:{i} from ({prev}) to ({cur})");

                        if (Math.Abs(prevRetY - tunit) > 0.0001)
                        {
                            var point = new CurvePoint()
                            {
                                XGrid = new XGrid(i, 0),
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
