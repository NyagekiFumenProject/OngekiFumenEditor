using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Core.Kernel.CurveInterpolater.DefaultImpl.Enumerator
{
    public class DefaultCurveInterpolateEnumerator : ICurveInterpolateEnumerator
    {
        private readonly LinkedList<CurvePoint> waiter = new LinkedList<CurvePoint>();
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
            itor = DistinctContinuousBy(children.SelectMany(Interpolate), (a, b) => a.TGrid == b.TGrid && a.XGrid == b.XGrid).GetEnumerator();
        }

        private static IEnumerable<T> DistinctContinuousBy<T>(IEnumerable<T> collection, System.Func<T, T, bool> compare)
        {
            using var enumerator = collection.GetEnumerator();
            if (!enumerator.MoveNext())
                yield break;

            var previous = enumerator.Current;
            yield return previous;

            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (!compare(previous, current))
                    yield return current;
                previous = current;
            }
        }

        protected virtual IEnumerable<CurvePoint> Interpolate(ConnectableChildObjectBase x)
        {
            CurvePoint build(Vector2 p)
            {
                var xGrid = new XGrid(p.X / x.XGrid.ResX);
                xGrid.NormalizeSelf();
                var tGrid = new TGrid(p.Y / x.TGrid.ResT);
                tGrid.NormalizeSelf();

                return new CurvePoint(tGrid, xGrid);
            }

            return x.GetConnectionPaths().Select(y => build(y.pos));
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
}
