using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Collections;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.Collections
{
	public class HoldList : IReadOnlyCollection<Hold>
	{
		private IntervalTreeWrapper<TGrid, Hold> startObjects = new(
			x => new() { Min = x.TGrid, Max = x.EndTGrid },
			true,
			nameof(Hold.TGrid),
			nameof(Hold.EndTGrid)
			);

		public int Count => startObjects.Count;

		public IEnumerator<Hold> GetEnumerator() => startObjects.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void Add(Hold obj)
		{
			startObjects.Add(obj);
		}

		public void Remove(Hold obj)
		{
			startObjects.Remove(obj);
		}

		public IEnumerable<Hold> GetVisibleStartObjects(TGrid min, TGrid max)
		{
			return startObjects.QueryInRange(min, max);
		}

		public bool Contains(Hold o)
		{
			return startObjects.FastContains(o);
		}
	}
}
