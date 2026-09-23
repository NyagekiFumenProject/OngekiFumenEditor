//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Base.Collections.Base.RangeTree
{
    /// <summary>
    ///     A node of the range tree. Given a list of items, it builds
    ///     its subtree. Also contains methods to query the subtree.
    ///     Basically, all interval tree logic is here.
    ///
    ///     节点在构建完成后不可变（字段全部 readonly，子树只建不删），因此任意数量的读者可以并发遍历同一棵树。
    ///     旧实现有一个 <c>Release</c>，重建时把旧节点递归就地清空——正在遍历的线程会解引用被置空的字段（RND-008 的 NRE）。
    ///     节点从不池化，就地清空省不下任何分配，只会把这个竞争窗口留在那里，故直接删除。
    /// </summary>
    internal sealed class IntervalTreeNode<TKey, TValue>
    {
        private readonly TKey center;
        private readonly IComparer<TKey> comparer;
        private readonly List<RangeValuePair<TKey, TValue>> items;
        private readonly IntervalTreeNode<TKey, TValue> leftNode;
        private readonly IntervalTreeNode<TKey, TValue> rightNode;

        public TKey Max { get; }
        public TKey Min { get; }

        private IntervalTreeNode(
            IComparer<TKey> comparer,
            TKey center,
            List<RangeValuePair<TKey, TValue>> items,
            IntervalTreeNode<TKey, TValue> leftNode,
            IntervalTreeNode<TKey, TValue> rightNode,
            TKey min,
            TKey max)
        {
            this.comparer = comparer ?? Comparer<TKey>.Default;
            this.center = center;
            this.items = items;
            this.leftNode = leftNode;
            this.rightNode = rightNode;
            Min = min;
            Max = max;
        }

        public static IntervalTreeNode<TKey, TValue> BuildTree(IEnumerable<RangeValuePair<TKey, TValue>> items, IComparer<TKey> comparer)
        {
            var nodeComparer = comparer ?? Comparer<TKey>.Default;
            var endPoints = new List<TKey>();
            foreach (var item in items)
            {
                endPoints.Add(item.From);
                endPoints.Add(item.To);
            }

            endPoints.Sort(nodeComparer);

            var center = default(TKey);
            var min = default(TKey);
            var max = default(TKey);
            if (endPoints.Count > 0)
            {
                min = endPoints[0];
                center = endPoints[endPoints.Count / 2];
                max = endPoints[endPoints.Count - 1];
            }

            var inner = new List<RangeValuePair<TKey, TValue>>();
            var left = new List<RangeValuePair<TKey, TValue>>();
            var right = new List<RangeValuePair<TKey, TValue>>();

            foreach (var value in items)
            {
                if (nodeComparer.Compare(value.To, center) < 0)
                    left.Add(value);
                else if (nodeComparer.Compare(value.From, center) > 0)
                    right.Add(value);
                else
                    inner.Add(value);
            }

            List<RangeValuePair<TKey, TValue>> innerItems;
            if (inner.Count > 0)
            {
                if (inner.Count > 1)
                    inner.Sort(new RangeValuePairComparer(nodeComparer));
                innerItems = inner;
            }
            else
            {
                innerItems = null;
            }

            var leftNode = left.Count > 0 ? BuildTree(left, nodeComparer) : null;
            var rightNode = right.Count > 0 ? BuildTree(right, nodeComparer) : null;

            return new IntervalTreeNode<TKey, TValue>(nodeComparer, center, innerItems, leftNode, rightNode, min, max);
        }

        /// <summary>按 (From, To) 排序 inner 列表；与旧实现的显式接口实现等价，只是不再让节点自身承担这个职责。</summary>
        private sealed class RangeValuePairComparer : IComparer<RangeValuePair<TKey, TValue>>
        {
            private readonly IComparer<TKey> comparer;

            public RangeValuePairComparer(IComparer<TKey> comparer) => this.comparer = comparer;

            public int Compare(RangeValuePair<TKey, TValue> x, RangeValuePair<TKey, TValue> y)
            {
                var fromComp = comparer.Compare(x.From, y.From);
                if (fromComp == 0)
                    return comparer.Compare(x.To, y.To);
                return fromComp;
            }
        }

        public IEnumerable<TValue> Query(TKey value)
        {
            var localItems = items;
            var localComparer = comparer;

            if (localItems != null)
            {
                foreach (var item in localItems)
                {
                    if (localComparer.Compare(item.From, value) > 0)
                        break;
                    if (localComparer.Compare(value, item.From) >= 0 && localComparer.Compare(value, item.To) <= 0)
                        yield return item.Value;
                }
            }

            var centerComp = localComparer.Compare(value, center);
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
            using var result = ObjectPool.GetPooledList<TValue>();

            QueryInto(from, to, result);
            foreach (var item in result)
                yield return item;
        }

        public void QueryInto(TKey from, TKey to, ICollection<TValue> output)
        {
            var localItems = items;
            var localComparer = comparer;

            if (localItems != null)
            {
                for (int i = 0; i < localItems.Count; i++)
                {
                    var item = localItems[i];
                    if (localComparer.Compare(item.From, to) > 0)
                        break;
                    if (localComparer.Compare(to, item.From) >= 0 && localComparer.Compare(from, item.To) <= 0)
                        output.Add(item.Value);
                }
            }

            if (leftNode != null && localComparer.Compare(from, center) < 0)
                leftNode.QueryInto(from, to, output);

            if (rightNode != null && localComparer.Compare(to, center) > 0)
                rightNode.QueryInto(from, to, output);
        }
    }
}
