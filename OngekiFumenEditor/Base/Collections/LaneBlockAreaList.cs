using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Collections;
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.Collections
{
	public class LaneBlockAreaList : IReadOnlyCollection<LaneBlockArea>
	{
		private IntervalTreeWrapper<TGrid, LaneBlockArea> blocks = new(
			x => new() { Min = x.TGrid, Max = x.EndIndicator.TGrid },
			true,
			nameof(LaneBlockArea.TGrid),
			nameof(LaneBlockArea.EndIndicator.TGrid)
			);

		public int Count => blocks.Count;

		public IEnumerator<LaneBlockArea> GetEnumerator() => blocks.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerable<LaneBlockArea> GetVisibleStartObjects(TGrid min, TGrid max)
		{
			return blocks.QueryInRange(min, max);
		}

		public void Add(LaneBlockArea laneBlock)
		{
			blocks.Add(laneBlock);
		}

		public void Remove(LaneBlockArea laneBlock)
		{
			blocks.Remove(laneBlock);
		}

		public bool Contains(LaneBlockArea o)
		{
			return blocks.Contains(o);
		}
	}
}
