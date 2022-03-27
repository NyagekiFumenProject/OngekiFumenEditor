using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public class CurveInterpolaterTraveller
    {
        public struct CurvePoint : ITimelineObject, IHorizonPositionObject
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

            public override string ToString() => $"{XGrid} {TGrid}";
        }

        private LinkedList<CurvePoint> waiter = new LinkedList<CurvePoint>();
        private IEnumerator<CurvePoint> itor;
        private readonly ConnectableStartObject start;

        public CurveInterpolaterTraveller(ConnectableStartObject start)
        {
            this.start = start;
            Reset();
        }

        public void Reset()
        {
            waiter.Clear();
            itor = start.Children.SelectMany(x => Interpolate(x)).DistinctBy((a,b) => {
                return a.TGrid == b.TGrid && a.XGrid == b.XGrid;
            }).GetEnumerator();
        }

        private IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
        {
            CurvePoint build(Vector2 p)
            {
                var xGrid = new XGrid(p.X / x.XGrid.ResX);
                xGrid.NormalizeSelf();
                var tGrid = new TGrid(p.Y / x.TGrid.ResT);
                tGrid.NormalizeSelf();

                return new(tGrid, xGrid);
            }

            using var d =
                x.PathControls
                .AsEnumerable<OngekiMovableObjectBase>()
                .Prepend(x.PrevObject)
                .Append(x)
                .Select(x => new Vector2(x.XGrid.TotalGrid, x.TGrid.TotalGrid))
                .ToListWithObjectPool(out var points);

            var t = 0f;
            var step = x.CurvePrecision;

            if (points.Count == 2)
            {
                yield return build(points[0]);
                yield return build(points[1]);
                yield break;
            }

            while (true)
            {
                var p = BezierCurve.CalculatePoint(points, t);

                yield return build(p);

                if (t >= 1)
                    break;

                t = MathF.Min(1, t + step);
            }
        }

        public CurvePoint? Travel()
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

        public void PushBack(CurvePoint point)
        {
            waiter.AddFirst(point);
        }
    }
}
