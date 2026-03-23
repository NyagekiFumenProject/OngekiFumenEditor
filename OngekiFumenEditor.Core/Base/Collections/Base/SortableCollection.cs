using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Base.Collections.Base
{
    public class SortableCollection<T, X> : IBinaryFindRangeEnumable<T, X> where X : IComparable<X>
    {
        private readonly List<T> items = new();
        private readonly Func<T, X> sortKeySelector;
        private readonly ComparerWrapper<T> comparer;

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
            {
                items.Add(obj);
                return;
            }

            var index = items.BinarySearch(obj, comparer);
            if (index < 0)
                index = ~index;
            items.Insert(index, obj);
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

            var minIndex = BinarySearchBy(min);
            minIndex = minIndex < 0 ? ~minIndex : minIndex;

            var maxIndex = BinarySearchBy(max, minIndex);
            maxIndex = maxIndex < 0 ? ~maxIndex : maxIndex + 1;

            return (minIndex, maxIndex);
        }

        public IEnumerable<T> BinaryFindRange(X min, X max)
        {
            var range = BinaryFindRangeIndex(min, max);
            for (var i = range.minIndex; i < range.maxIndex; i++)
                yield return items[i];
        }

        public int BinarySearchBy(X key)
        {
            return BinarySearchBy(key, 0);
        }

        private int BinarySearchBy(X key, int startIndex)
        {
            return BinarySearchBy(items, key, sortKeySelector, startIndex);
        }

        public int BinaryFindLastIndexByKey(X key)
        {
            var minIndex = BinarySearchBy(key);
            minIndex = minIndex < 0 ? ~minIndex : minIndex;

            return minIndex;
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

        private static int BinarySearchBy(IList<T> source, X value, Func<T, X> keySelector, int lo = 0)
        {
            var hi = source.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var currentValue = keySelector(source[i]);
                var order = currentValue.CompareTo(value);

                if (order == 0)
                {
                    for (var r = i + 1; r < source.Count; r++)
                    {
                        var nextValue = keySelector(source[r]);
                        if (nextValue.CompareTo(currentValue) == 0)
                            i = r;
                        else
                            break;
                    }
                    return i;
                }

                if (order < 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return ~lo;
        }
    }
}

