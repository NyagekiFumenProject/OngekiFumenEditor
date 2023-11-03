using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections.Base
{
	public class SortableCollection<T, X> : IBinaryFindRangeEnumable<T, X> where X : IComparable<X>
	{
		private List<T> items = new();
		private readonly Func<T, X> sortKeySelector;
		private ComparerWrapper<T> comparer;

		public bool IsBatching { get; private set; }
		public int Count => items.Count;

		public int Capacity
		{
			get => items.Capacity;
			set => items.Capacity = value;
		}

		public T this[int i] => items[i];

		public SortableCollection(Func<T, X> sortKeySelector)
		{
			comparer = new ComparerWrapper<T>((a, b) => sortKeySelector(a).CompareTo(sortKeySelector(b)));
			this.sortKeySelector = sortKeySelector;
		}

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public virtual void Add(T obj)
		{
			if (IsBatching)
				items.Add(obj);
			else
			{
				var index = items.BinarySearch(obj, comparer);
				if (index < 0)
					index = ~index;
				items.Insert(index, obj);
			}
		}

		public virtual bool Remove(T obj)
		{
			return items.Remove(obj);
		}

		public bool Contains(T obj)
		{
#if DEBUG
			if (IsBatching)
				throw new Exception("Collection is in batching....");
#endif
			var index = items.BinarySearch(obj, comparer);
			return index >= 0;
		}

		public void BeginBatchAction()
		{
			IsBatching = true;
		}

		public void EndBatchAction()
		{
			IsBatching = false;
			items.Sort(comparer);
		}

		public (int minIndex, int maxIndex) BinaryFindRangeIndex(X min, X max)
		{
#if DEBUG
			if (IsBatching)
				throw new Exception("Collection is in batching....");
#endif

			var minIndex = items.BinarySearchBy(min, sortKeySelector);
			minIndex = minIndex < 0 ? ~minIndex : minIndex;
			var maxIndex = items.BinarySearchBy(max, sortKeySelector, minIndex);
			maxIndex = maxIndex < 0 ? ~maxIndex : maxIndex + 1;

			return (minIndex, maxIndex);
		}

		public IEnumerable<T> BinaryFindRange(X min, X max)
		{
			var range = BinaryFindRangeIndex(min, max);
			return Enumerable.Range(range.minIndex, range.maxIndex - range.minIndex).Select(i => items[i]);
		}

		public int BinaryFindLastIndexByKey(X key)
		{
			var minIndex = items.BinarySearchBy(key, sortKeySelector);
			minIndex = minIndex < 0 ? ~minIndex : minIndex;

			return minIndex;
		}

		public void Clear()
		{
			items.Clear();
		}
	}
}
