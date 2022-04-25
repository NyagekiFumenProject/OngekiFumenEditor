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
            itor = start.Children.SelectMany(x => Interpolate(x)).DistinctContinuousBy((a,b) => {
                return a.TGrid == b.TGrid && a.XGrid == b.XGrid;
            }).GetEnumerator();
        }

        protected virtual IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
        {
            CurvePoint build(Vector2 p)
            {
                var xGrid = new XGrid(p.X / x.XGrid.ResX);
                xGrid.NormalizeSelf();
                var tGrid = new TGrid(p.Y / x.TGrid.ResT);
                tGrid.NormalizeSelf();

                return new(tGrid, xGrid);
            }

            return x.GenPath().Select(x => build(x.pos));
        }

        public virtual CurvePoint? Travel()
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
