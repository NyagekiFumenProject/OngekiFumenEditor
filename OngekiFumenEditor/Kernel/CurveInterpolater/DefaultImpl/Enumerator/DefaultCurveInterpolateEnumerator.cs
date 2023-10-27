using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Enumerator
{
	public class DefaultCurveInterpolateEnumerator : ICurveInterpolateEnumerator
	{
		private LinkedList<CurvePoint> waiter = new LinkedList<CurvePoint>();
		private IEnumerator<CurvePoint> itor;

		public DefaultCurveInterpolateEnumerator(ConnectableStartObject start) : this(start.Children.FirstOrDefault(), default)
		{

		}

		public DefaultCurveInterpolateEnumerator(ConnectableChildObjectBase from, ConnectableChildObjectBase to = default)
		{
			waiter.Clear();

			var children = from.ReferenceStartObject.Children
				.SkipWhile(x => x != from)
				.TakeWhile(x => x != to).ToArray();
			itor = children
				.SelectMany(x => Interpolate(x))
				.DistinctContinuousBy((a, b) =>
				{
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

			return x.GetConnectionPaths().Select(x => build(x.pos));
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
