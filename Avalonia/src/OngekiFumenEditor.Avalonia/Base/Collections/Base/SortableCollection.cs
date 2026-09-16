using OngekiFumenEditor.Avalonia.Utils;
using System.Collections;

namespace OngekiFumenEditor.Avalonia.Base.Collections.Base
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
            var (minIndex, maxIndex) = BinaryFindRangeIndex(min, max);
            for (var i = minIndex; i < maxIndex; i++)
                yield return items[i];
        }

        public int BinarySearchBy(X key)
        {
            return items.BinarySearchBy(key, sortKeySelector);
        }

        public int BinaryFindLastIndexByKey(X key)
        {
            var minIndex = items.BinarySearchBy(key, sortKeySelector);
            minIndex = minIndex < 0 ? ~minIndex : minIndex;

            return minIndex;
        }

        /// <summary>
        /// 返回第一个排序键 &gt;= <paramref name="key"/> 的元素下标。若所有元素的键都 &lt; <paramref name="key"/>，
        /// 则返回 <see cref="Count"/>。二分查找，O(log n)。
        /// </summary>
        public int LowerBoundIndex(X key)
        {
#if DEBUG
            if (IsBatching)
                throw new Exception("Collection is in batching....");
#endif
            var lo = 0;
            var hi = items.Count;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (sortKeySelector(items[mid]).CompareTo(key) < 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// 返回第一个排序键 &gt; <paramref name="key"/> 的元素下标。若所有元素的键都 &lt;= <paramref name="key"/>，
        /// 则返回 <see cref="Count"/>。二分查找，O(log n)。
        /// </summary>
        public int UpperBoundIndex(X key)
        {
#if DEBUG
            if (IsBatching)
                throw new Exception("Collection is in batching....");
#endif
            var lo = 0;
            var hi = items.Count;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (sortKeySelector(items[mid]).CompareTo(key) <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        public void Clear()
        {
            items.Clear();
        }

        public T RemoveAt(int idx)
        {
            var obj = items[idx];
            items.RemoveAt(idx);
            return obj;
        }
    }
}

