//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Generic;

namespace IntervalTree
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

		/// <summary>
		///     Initializes an empty node.
		/// </summary>
		/// <param name="comparer">The comparer used to compare two items.</param>
		public IntervalTreeNode(IComparer<TKey> comparer) : this()
		{
			this.comparer = comparer ?? Comparer<TKey>.Default;
		}

		void ClearValue()
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

			ObjectPool<List<RangeValuePair<TKey, TValue>>>.Return(unusedObj.items);
			unusedObj.ClearValue();
			ObjectPool<IntervalTreeNode<TKey, TValue>>.Return(unusedObj);
		}

		/// <summary>
		///     Initializes a node with a list of items, builds the sub tree.
		/// </summary>
		/// <param name="items">The items that should be added to this node</param>
		/// <param name="comparer">The comparer used to compare two items.</param>
		public static IntervalTreeNode<TKey, TValue> BuildTree(IEnumerable<RangeValuePair<TKey, TValue>> items, IComparer<TKey> comparer)
		{
			var node = ObjectPool<IntervalTreeNode<TKey, TValue>>.Get();
			node.comparer = comparer ?? Comparer<TKey>.Default;

			// first, find the median
			var endPoints = ObjectPool<List<TKey>>.Get();
			endPoints.Clear();

			foreach (var item in items)
			{
				endPoints.Add(item.From);
				endPoints.Add(item.To);
			}

			endPoints.Sort(node.comparer);

			// the median is used as center value
			if (endPoints.Count > 0)
			{
				node.Min = endPoints[0];
				node.center = endPoints[endPoints.Count / 2];
				node.Max = endPoints[endPoints.Count - 1];
			}
			ObjectPool<List<TKey>>.Return(endPoints);

			var inner = ObjectPool<List<RangeValuePair<TKey, TValue>>>.Get();
			var left = ObjectPool<List<RangeValuePair<TKey, TValue>>>.Get();
			var right = ObjectPool<List<RangeValuePair<TKey, TValue>>>.Get();
			inner.Clear();
			left.Clear();
			right.Clear();

			// iterate over all items
			// if the range of an item is completely left of the center, add it to the left items
			// if it is on the right of the center, add it to the right items
			// otherwise (range overlaps the center), add the item to this node's items
			foreach (var o in items)
				if (node.comparer.Compare(o.To, node.center) < 0)
					left.Add(o);
				else if (node.comparer.Compare(o.From, node.center) > 0)
					right.Add(o);
				else
					inner.Add(o);

			// sort the items, this way the query is faster later on
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

			// create left and right nodes, if there are any items
			if (left.Count > 0)
				node.leftNode = BuildTree(left, node.comparer);
			if (right.Count > 0)
				node.rightNode = BuildTree(right, node.comparer);

			ObjectPool<List<RangeValuePair<TKey, TValue>>>.Return(left);
			ObjectPool<List<RangeValuePair<TKey, TValue>>>.Return(right);
			return node;
		}

		/// <summary>
		///     Returns less than 0 if this range's From is less than the other, greater than 0 if greater.
		///     If both are equal, the comparison of the To values is returned.
		///     0 if both ranges are equal.
		/// </summary>
		/// <param name="x">The first item.</param>
		/// <param name="y">The other item.</param>
		/// <returns></returns>
		int IComparer<RangeValuePair<TKey, TValue>>.Compare(RangeValuePair<TKey, TValue> x,
			RangeValuePair<TKey, TValue> y)
		{
			var fromComp = comparer.Compare(x.From, y.From);
			if (fromComp == 0)
				return comparer.Compare(x.To, y.To);
			return fromComp;
		}

		/// <summary>
		///     Performs a point query with a single value.
		///     All items with overlapping ranges are returned.
		/// </summary>
		public IEnumerable<TValue> Query(TKey value)
		{
			// If the node has items, check for leaves containing the value.
			if (items != null)
				foreach (var o in items)
					if (comparer.Compare(o.From, value) > 0)
						break;
					else if (comparer.Compare(value, o.From) >= 0 && comparer.Compare(value, o.To) <= 0)
						yield return o.Value;

			// go to the left or go to the right of the tree, depending
			// where the query value lies compared to the center
			var centerComp = comparer.Compare(value, center);
			if (leftNode != null && centerComp < 0)
				foreach (var item in leftNode.Query(value))
					yield return item;
			else if (rightNode != null && centerComp > 0)
				foreach (var item in rightNode.Query(value))
					yield return item;
		}

		/// <summary>
		///     Performs a range query.
		///     All items with overlapping ranges are returned.
		/// </summary>
		public IEnumerable<TValue> Query(TKey from, TKey to)
		{
			// If the node has items, check for leaves intersecting the range.
			if (items != null)
				foreach (var o in items)
					if (comparer.Compare(o.From, to) > 0)
						break;
					else if (comparer.Compare(to, o.From) >= 0 && comparer.Compare(from, o.To) <= 0)
						yield return o.Value;

			// go to the left or go to the right of the tree, depending
			// where the query value lies compared to the center
			if (leftNode != null && comparer.Compare(from, center) < 0)
				foreach (var item in leftNode.Query(from, to))
					yield return item;
			if (rightNode != null && comparer.Compare(to, center) > 0)
				foreach (var item in rightNode.Query(from, to))
					yield return item;
		}
	}
}