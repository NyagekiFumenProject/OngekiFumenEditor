//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Core.Base.Collections.Base.RangeTree
{
    public class IntervalTree<TKey, TValue> : IIntervalTree<TKey, TValue>
    {
        private IntervalTreeNode<TKey, TValue> root;
        private List<RangeValuePair<TKey, TValue>> items = new();
        private readonly IComparer<TKey> comparer;
        private bool isInSync;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool EnableAutoSwapMinMax { get; set; } = false;

        public TKey Max
        {
            get
            {
                if (!isInSync)
                    RebuildInternal();

                return root.Max;
            }
        }

        public TKey Min
        {
            get
            {
                if (!isInSync)
                    RebuildInternal();

                return root.Min;
            }
        }

        public IEnumerable<TValue> Values => items.Select(i => i.Value);

        public int Count => items.Count;

        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree() : this(Comparer<TKey>.Default) { }

        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree(IComparer<TKey> comparer)
        {
            this.comparer = comparer ?? Comparer<TKey>.Default;
            isInSync = true;
            root = new IntervalTreeNode<TKey, TValue>(this.comparer);
        }

        public IEnumerable<TValue> Query(TKey value)
        {
            if (!isInSync)
                RebuildInternal();

            return root.Query(value);
        }

        public IEnumerable<TValue> Query(TKey from, TKey to)
        {
            if (!isInSync)
                RebuildInternal();

            return root.Query(from, to);
        }

        public void Add(TKey from, TKey to, TValue value)
        {
            var index = items.FindIndex(x => x.Value.Equals(value));
            if (index > -1)
                throw new ArgumentOutOfRangeException($"{value} is exist");

            if (comparer.Compare(from, to) > 0)
            {
                if (EnableAutoSwapMinMax)
                    (to, from) = (from, to);
                else
                    throw new ArgumentOutOfRangeException($"{nameof(from)} cannot be larger than {nameof(to)}");
            }

            NotifyDirty();
            items.Add(new RangeValuePair<TKey, TValue>(from, to, value));
        }

        public bool Remove(TValue value)
        {
            var index = items.FindIndex(x => x.Value.Equals(value));
            if (index < 0)
                return false;
            NotifyDirty();
            items.RemoveAt(index);
            return true;
        }

        public void Clear()
        {
            IntervalTreeNode<TKey, TValue>.Release(root);
            root = new IntervalTreeNode<TKey, TValue>(comparer);
            items.Clear();
            isInSync = true;
        }

        public IEnumerator<RangeValuePair<TKey, TValue>> GetEnumerator()
        {
            if (!isInSync)
                RebuildInternal();

            return items.GetEnumerator();
        }

        public void NotifyDirty() => isInSync = false;

        private void RebuildInternal()
        {
            if (isInSync)
                return;

            IntervalTreeNode<TKey, TValue>.Release(root);
            root = IntervalTreeNode<TKey, TValue>.BuildTree(items, comparer);
            isInSync = true;
        }
    }
}