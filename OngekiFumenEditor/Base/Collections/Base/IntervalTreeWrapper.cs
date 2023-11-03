using IntervalTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections.Base
{
	public class IntervalTreeWrapper<TKey, TValue> : IReadOnlyCollection<TValue> where TValue : INotifyPropertyChanged where TKey : IComparable<TKey>
	{
		private IIntervalTree<TKey, TValue> tree;
		private readonly Func<TValue, KeyRange> rangeKeySelector;
		private readonly HashSet<string> rebuildProperties;

		public bool IsBatching { get; private set; }
		public int Count => tree.Count;

		public struct KeyRange
		{
			public TKey Min { get; set; }
			public TKey Max { get; set; }
		}

		public IEnumerator<TValue> GetEnumerator() => tree.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IntervalTreeWrapper(Func<TValue, KeyRange> rangeKeySelector, bool enableSwap, params string[] rebuildProperties)
		{
			this.rangeKeySelector = rangeKeySelector;
			this.rebuildProperties = rebuildProperties.ToHashSet();
			tree = new IntervalTree<TKey, TValue>() { EnableAutoSwapMinMax = enableSwap };
		}

		public void Add(TValue obj)
		{
			var keyRange = rangeKeySelector(obj);
			tree.Add(keyRange.Min, keyRange.Max, obj);

			obj.PropertyChanged += OnItemPropChanged;
		}

		private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
		{
			if (rebuildProperties.Contains(e.PropertyName))
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
			if (obj is null)
				return false;

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
