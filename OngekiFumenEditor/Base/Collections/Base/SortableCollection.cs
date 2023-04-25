using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections.Base
{
    public class SortableCollection<T, X> : IEnumerable<T> where T : INotifyPropertyChanged where X : IComparable<X>
    {
        private List<T> items = new List<T>();
        private readonly Func<T, X> sortKeySelector;
        private readonly string sortKeyPropertyName;
        private ComparerWrapper<T> comparer;

        public bool IsBatching { get; private set; }
        public int Count => items.Count;

        public SortableCollection(Func<T, X> sortKeySelector, string sortKeyPropertyName = default)
        {
            comparer = new ComparerWrapper<T>((a, b) => sortKeySelector(a).CompareTo(sortKeySelector(b)));
            this.sortKeySelector = sortKeySelector;
            this.sortKeyPropertyName = sortKeyPropertyName;
        }

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(T obj)
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

            obj.PropertyChanged += OnItemPropChanged;
        }

        private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsBatching)
                return;

            if (e.PropertyName == sortKeyPropertyName)
            {
                var obj = (T)sender;
                Remove(obj);
                Add(obj);
            }
        }

        public void Remove(T obj)
        {
            items.Remove(obj);
            obj.PropertyChanged -= OnItemPropChanged;
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

        public IEnumerable<T> BinaryFindRange(X min, X max)
        {
#if DEBUG
            if (IsBatching)
                throw new Exception("Collection is in batching....");
#endif

            var minIndex = items.BinarySearchBy(min, sortKeySelector);
            minIndex = minIndex < 0 ? ~minIndex : minIndex;
            var maxIndex = items.BinarySearchBy(max, sortKeySelector, minIndex);
            maxIndex = maxIndex < 0 ? ~maxIndex : maxIndex + 1;

            return Enumerable.Range(minIndex, maxIndex - minIndex).Select(i => items[i]);
        }

        public bool FastContains(T obj) => Contains(obj);
    }
}
