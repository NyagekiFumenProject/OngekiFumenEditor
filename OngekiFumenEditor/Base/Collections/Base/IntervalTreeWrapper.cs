using IntervalTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections.Base
{
    public class IntervalTreeWrapper<TKey, TValue> : IEnumerable<TValue> where TValue : INotifyPropertyChanged where TKey : IComparable<TKey>
    {
        private IIntervalTree<TKey, TValue> tree;
        private readonly Func<TValue, KeyRange> rangeKeySelector;
        private readonly string sortMinKeyPropertyName;
        private readonly string sortMaxKeyPropertyName;

        public bool IsBatching { get; private set; }

        public struct KeyRange
        {
            public TKey Min { get; set; }
            public TKey Max { get; set; }
        }

        public IEnumerator<TValue> GetEnumerator() => tree.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IntervalTreeWrapper(Func<TValue, KeyRange> rangeKeySelector, string sortMinKeyPropertyName = default, string sortMaxKeyPropertyName = default)
        {
            this.rangeKeySelector = rangeKeySelector;
            this.sortMinKeyPropertyName = sortMinKeyPropertyName;
            this.sortMaxKeyPropertyName = sortMaxKeyPropertyName;

            tree = new IntervalTree<TKey, TValue>();
        }

        public void Add(TValue obj)
        {
            var keyRange = rangeKeySelector(obj);
            tree.Add(keyRange.Min, keyRange.Max, obj);

            obj.PropertyChanged += OnItemPropChanged;
        }

        private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == sortMinKeyPropertyName || e.PropertyName == sortMaxKeyPropertyName)
            {
                if (IsBatching)
                    return; //not to process dirty data yet.

                var obj = (TValue)sender;
                Remove(obj);
                Add(obj);
            }
        }

        public void Remove(TValue obj)
        {
            tree.Remove(obj);
            obj.PropertyChanged -= OnItemPropChanged;
        }

        public bool Contains(TValue obj)
        {
#if DEBUG
            if (IsBatching)
                throw new Exception("Collection is in batching....");
#endif
            var keyRange = rangeKeySelector(obj);
            return tree.Query(keyRange.Min, keyRange.Max).Contains(obj);
        }

        public IEnumerable<TValue> QueryInRange(TKey min, TKey max)
        {
#if DEBUG
            if (IsBatching)
                throw new Exception("Collection is in batching....");
#endif
            return tree.Query(min, max);
        }

        public void BeginBatchAction()
        {
            IsBatching = true;
        }

        public void EndBatchAction()
        {
            IsBatching = false;
            tree.NotifyDirty();
        }

        public bool FastContains(TValue obj) => Contains(obj);
    }
}
