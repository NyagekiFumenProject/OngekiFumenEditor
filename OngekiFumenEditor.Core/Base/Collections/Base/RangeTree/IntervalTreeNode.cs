//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using System.Collections.Generic;

namespace OngekiFumenEditor.Base.Collections.Base.RangeTree
{
    /// <summary>
    ///     A node of the range tree. Given a list of items, it builds
    ///     its subtree. Also contains methods to query the subtree.
    ///     Basically, all interval tree logic is here.
    /// </summary>
    internal class IntervalTreeNode<TKey, TValue> : IComparer<RangeValuePair<TKey, TValue>>
    {
        private TKey center;
        private IComparer<TKey> comparer;
        private List<RangeValuePair<TKey, TValue>> items;
        private IntervalTreeNode<TKey, TValue> leftNode;
        private IntervalTreeNode<TKey, TValue> rightNode;

        public TKey Max { get; private set; }
        public TKey Min { get; private set; }

        public IntervalTreeNode()
        {
            ClearValue();
        }

        public IntervalTreeNode(IComparer<TKey> comparer) : this()
        {
            this.comparer = comparer ?? Comparer<TKey>.Default;
        }

        private void ClearValue()
        {
            comparer = default;
            leftNode = default;
            rightNode = default;
            center = default;
            items = default;
            Min = default;
            Max = default;
        }

        public static void Release(IntervalTreeNode<TKey, TValue> unusedObj)
        {
            if (unusedObj == null)
                return;

            Release(unusedObj.leftNode);
            Release(unusedObj.rightNode);
            unusedObj.ClearValue();
        }

        public static IntervalTreeNode<TKey, TValue> BuildTree(IEnumerable<RangeValuePair<TKey, TValue>> items, IComparer<TKey> comparer)
        {
            var node = new IntervalTreeNode<TKey, TValue>();
            node.comparer = comparer ?? Comparer<TKey>.Default;

            var endPoints = new List<TKey>();
            foreach (var item in items)
            {
                endPoints.Add(item.From);
                endPoints.Add(item.To);
            }

            endPoints.Sort(node.comparer);
            if (endPoints.Count > 0)
            {
                node.Min = endPoints[0];
                node.center = endPoints[endPoints.Count / 2];
                node.Max = endPoints[endPoints.Count - 1];
            }

            var inner = new List<RangeValuePair<TKey, TValue>>();
            var left = new List<RangeValuePair<TKey, TValue>>();
            var right = new List<RangeValuePair<TKey, TValue>>();

            foreach (var value in items)
            {
                if (node.comparer.Compare(value.To, node.center) < 0)
                    left.Add(value);
                else if (node.comparer.Compare(value.From, node.center) > 0)
                    right.Add(value);
                else
                    inner.Add(value);
            }

            if (inner.Count > 0)
            {
                if (inner.Count > 1)
                    inner.Sort(node);
                node.items = inner;
            }
            else
            {
                node.items = null;
            }

            if (left.Count > 0)
                node.leftNode = BuildTree(left, node.comparer);
            if (right.Count > 0)
                node.rightNode = BuildTree(right, node.comparer);

            return node;
        }

        int IComparer<RangeValuePair<TKey, TValue>>.Compare(RangeValuePair<TKey, TValue> x, RangeValuePair<TKey, TValue> y)
        {
            var fromComp = comparer.Compare(x.From, y.From);
            if (fromComp == 0)
                return comparer.Compare(x.To, y.To);
            return fromComp;
        }

        public IEnumerable<TValue> Query(TKey value)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (comparer.Compare(item.From, value) > 0)
                        break;
                    if (comparer.Compare(value, item.From) >= 0 && comparer.Compare(value, item.To) <= 0)
                        yield return item.Value;
                }
            }

            var centerComp = comparer.Compare(value, center);
            if (leftNode != null && centerComp < 0)
            {
                foreach (var item in leftNode.Query(value))
                    yield return item;
            }
            else if (rightNode != null && centerComp > 0)
            {
                foreach (var item in rightNode.Query(value))
                    yield return item;
            }
        }

        public IEnumerable<TValue> Query(TKey from, TKey to)
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (comparer.Compare(item.From, to) > 0)
                        break;
                    if (comparer.Compare(to, item.From) >= 0 && comparer.Compare(from, item.To) <= 0)
                        yield return item.Value;
                }
            }

            if (leftNode != null && comparer.Compare(from, center) < 0)
            {
                foreach (var item in leftNode.Query(from, to))
                    yield return item;
            }

            if (rightNode != null && comparer.Compare(to, center) > 0)
            {
                foreach (var item in rightNode.Query(from, to))
                    yield return item;
            }
        }
    }
}
