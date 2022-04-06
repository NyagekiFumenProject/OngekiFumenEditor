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
            var sameXUnitGroup = new List<CurvePoint>();
            var currentCheckXUnit = -2857;

            IEnumerable<CurvePoint> ProcessGroup()
            {
                if (sameXUnitGroup.Count <= 1)
                {
                    foreach (var item in sameXUnitGroup)
                        yield return item;
                    yield break;
                }
                /*
                    |
                    |  /
                    | /
                    |/ <--- 保留这个
                   /|
                  / |
                  \ |
                   \| <--- 保留这个  
                    |\   
                    | \
                    |  \
                 */

                foreach (var points in sameXUnitGroup.SplitByTurningGradient(x => x.XGrid.TotalGrid))
                {
                    yield return points.MinBy(x =>
                    {
                        var xuint = x.XGrid.TotalGrid * 1.0f / x.XGrid.ResX;
                        return Math.Abs(xuint - (int)xuint);
                    });
                }
            }

            foreach (var curvePoint in base.Interpolate(x))
            {
                var xunit = (int)Math.Floor(curvePoint.XGrid.TotalGrid * 1f / curvePoint.XGrid.ResX + 0.5f);
                if (currentCheckXUnit != xunit)
                {
                    Log.LogDebug($"Process xunit:{currentCheckXUnit} count:{sameXUnitGroup.Count}");
                    foreach (var cp in ProcessGroup())
                        yield return cp;
                    sameXUnitGroup.Clear();
                }

                sameXUnitGroup.Add(curvePoint);
                currentCheckXUnit = xunit;
            }

            foreach (var curvePoint in ProcessGroup())
                yield return curvePoint;
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
