using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils
{
	public static class LinqExtensionMethod
	{
		public static IEnumerable<T> OrderBy<T>(this IEnumerable<T> list, params Func<T, T, int>[] cmpFuncs)
		{
			var comparer = new ComparerWrapper<T>((a, b) =>
			{
				foreach (var cmpFunc in cmpFuncs)
				{
					var result = cmpFunc(a, b);
					if (result != 0)
						return result;
				}
				return 0;
			});
			return list.OrderBy(x => x, comparer);
		}

		/// <summary>
		/// 重复某个元素
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="o"></param>
		/// <param name="repeatCount"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> Repeat<T>(this T o, int repeatCount) => Enumerable.Repeat(o, repeatCount);

		public static void ForEach<T>(this IEnumerable<T> list, Action<T> fun)
		{
			if (list is null)
				return;
			foreach (var item in list)
				fun(item);
		}

		public static bool TryElementAt<T>(this T[] list, int idx, out T element)
		{
			if (list.Length >= (idx + 1))
			{
				element = list[idx];
				return true;
			}

			element = default(T);
			return false;
		}

		/// <summary>
		/// 使用默认比较器，过滤掉重复的元素
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		public static void DistinctSelf<T>(this ICollection<T> collection)
		{
			using var d = collection.Except(collection.Distinct()).ToListWithObjectPool(out var removes);
			foreach (var rm in removes)
				collection.Remove(rm);
		}

		/// <summary>
		/// 使用默认的比较器，根据某个条件进行过滤
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="Y"></typeparam>
		/// <param name="collection"></param>
		/// <param name="keySelect"></param>
		public static void DistinctBySelf<T, Y>(this ICollection<T> collection, Func<T, Y> keySelect)
		{
			using var d = collection.Except(collection.DistinctBy(keySelect)).ToListWithObjectPool(out var removes);
			foreach (var rm in removes)
				collection.Remove(rm);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveRange<T>(this Collection<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Remove(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void RemoveRange<T>(this HashSet<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Remove(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddRange<T>(this Collection<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Add(x));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void AddRange<T>(this HashSet<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Add(x));

		/*
        public static IEnumerable<T> DistinctBy<T, Y>(this IEnumerable<T> collection, Func<T, Y> keySelect) => collection.DistinctBy((a, b) => keySelect(a)?.Equals(keySelect(b)) ?? keySelect(b)?.Equals(keySelect(a)) ?? true);

        public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> collection, Func<T, T, bool> compFunc) => collection.Distinct(new FuncDistinctComparer<T>(compFunc));
        */
		/// <summary>
		/// Distinct continuous same values.
		/// example : 1,2,3,3,4,1,1,2,3 -> 1,2,3,4,1,2,3
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="compFunc"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> DistinctContinuousBy<T, Y>(this IEnumerable<T> collection, Func<T, Y> keySelect) => collection.DistinctContinuousBy((a, b) => keySelect(a)?.Equals(keySelect(b)) ?? keySelect(b)?.Equals(keySelect(a)) ?? true);

		/// <summary>
		/// Distinct continuous same values.
		/// example : 1,2,3,3,4,1,1,2,3 -> 1,2,3,4,1,2,3
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="compFunc"></param>
		/// <returns></returns>
		public static IEnumerable<T> DistinctContinuousBy<T>(this IEnumerable<T> collection, Func<T, T, bool> compFunc)
		{
			var itor = collection.GetEnumerator();
			bool isFirst = true;
			var prev = default(T);

			while (itor.MoveNext())
			{
				var value = itor.Current;
				if (isFirst)
				{
					yield return value;
					isFirst = false;
				}
				else if (!compFunc(prev, value))
					yield return value;
				prev = value;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> FilterNull<T>(this IEnumerable<T> collection) => collection.Where(x => x != null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> FilterNullBy<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector) => collection.Where(x => keySelector(x) != null);

		public static T FindNextOrDefault<T>(this IEnumerable<T> collection, T taget)
		{
			var itor = collection.GetEnumerator();
			var prev = default(T);

			while (itor.MoveNext())
			{
				if (taget.Equals(prev))
					return itor.Current;
				prev = itor.Current;
			}

			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MinByOrDefault<T>(this IEnumerable<T> collection)
			=> collection.MinByOrDefault(x => x, Comparer<T>.Default.Compare);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MinByOrDefault<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector = default) where X : IComparable<X>
			=> collection.MinByOrDefault((a, b) => keySelector(a).CompareTo(keySelector(b)));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MinByOrDefault<T>(this IEnumerable<T> collection, Func<T, T, int> comparer = default)
			=> collection.MinByOrDefault(x => x, comparer);
		public static T MinByOrDefault<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector, Func<X, X, int> comparer)
		{
			var itor = collection.GetEnumerator();
			if (!itor.MoveNext())
				return default;

			var first = itor.Current;
			X min = keySelector(first);
			T minValue = first;

			while (itor.MoveNext())
			{
				var item = itor.Current;

				var v = keySelector(item);
				if (comparer(min, v) > 0)
				{
					min = v;
					minValue = item;
				}
			}

			return minValue;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MaxByOrDefault<T>(this IEnumerable<T> collection)
			=> collection.MaxByOrDefault(x => x, Comparer<T>.Default.Compare);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MaxByOrDefault<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector = default) where X : IComparable<X>
			=> collection.MaxByOrDefault((a, b) => keySelector(a).CompareTo(keySelector(b)));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T MaxByOrDefault<T>(this IEnumerable<T> collection, Func<T, T, int> comparer = default)
			=> collection.MaxByOrDefault(x => x, comparer);
		public static T MaxByOrDefault<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector, Func<X, X, int> comparer)
		{
			var itor = collection.GetEnumerator();
			if (!itor.MoveNext())
				return default;

			var first = itor.Current;
			X max = keySelector(first);
			T maxValue = first;

			while (itor.MoveNext())
			{
				var item = itor.Current;

				var v = keySelector(item);
				if (comparer(max, v) < 0)
				{
					max = v;
					maxValue = item;
				}
			}

			return maxValue;
		}

		public static T FindPreviousOrDefault<T>(this IEnumerable<T> collection, T taget)
		{
			var itor = collection.GetEnumerator();
			var prev = default(T);

			while (itor.MoveNext())
			{
				if (itor.Current.Equals(taget))
					return prev;
				prev = itor.Current;
			}

			return default;
		}

		public static IDisposable ToListWithObjectPool<T>(this IEnumerable<T> collection, out List<T> list)
		{
			var disposable = ObjectPool<List<T>>.GetWithUsingDisposable(out list, out _);
			list.Clear();
			list.AddRange(collection);
			return disposable;
		}

		public static IDisposable ToHashSetWithObjectPool<T>(this IEnumerable<T> collection, out HashSet<T> set)
		{
			var disposable = ObjectPool<HashSet<T>>.GetWithUsingDisposable(out set, out _);
			set.Clear();
			set.AddRange(collection);
			return disposable;
		}

		public static IDisposable ToDictionaryWithObjectPool<T, K, V>(this IEnumerable<T> collection, Func<T, K> keySelector, Func<T, V> valueSelector, out Dictionary<K, V> dic)
		{
			var disposable = ObjectPool<Dictionary<K, V>>.GetWithUsingDisposable(out dic, out _);
			dic.Clear();
			foreach (var item in collection)
				dic[keySelector(item)] = valueSelector(item);
			return disposable;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Sort<T>(this List<T> list, Func<T, T, int> compFunc) => list.Sort(new ComparerWrapper<T>(compFunc));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SortBy<T, X>(this List<T> list, Func<T, X> keySelect) => list.Sort((a, b) => Comparer<X>.Default.Compare(keySelect(a), keySelect(b)));

		public static int BinarySearchBy<T, X>(this IList<T> insertable, X value, Func<T, X> keySelect, int lo = 0) where X : IComparable<X>
		{
			//https://referencesource.microsoft.com/mscorlib/system/collections/generic/arraysorthelper.cs.html#f3d6c6df965a8a86

			int hi = insertable.Count - 1;
			while (lo <= hi)
			{
				int i = lo + ((hi - lo) >> 1);
				var val = keySelect(insertable[i]);
				int order = val.CompareTo(value);

				if (order == 0)
				{
					//考虑到出现重复值
					for (int r = i + 1; r < insertable.Count; r++)
					{
						var v = keySelect(insertable[r]);
						if (v.CompareTo(val) == 0)
							i = r;
						else
							break;
					}
					return i;
				}
				if (order < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i - 1;
				}
			}

			return ~lo;
		}

		private static int BinarySearchBy<T, X>(this IReadOnlyList<T> insertable, X value, Func<T, X> keySelect, int lo = 0) where X : IComparable<X>
		{
			//https://referencesource.microsoft.com/mscorlib/system/collections/generic/arraysorthelper.cs.html#f3d6c6df965a8a86

			int hi = insertable.Count - 1;
			while (lo <= hi)
			{
				int i = lo + ((hi - lo) >> 1);
				var val = keySelect(insertable[i]);
				int order = val.CompareTo(value);

				if (order == 0)
				{
					//考虑到出现重复值
					for (int r = i + 1; r < insertable.Count; r++)
					{
						var v = keySelect(insertable[r]);
						if (v.CompareTo(val) == 0)
							i = r;
						else
							break;
					}
					return i;
				}
				if (order < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i - 1;
				}
			}

			return ~lo;
		}

		/// <summary>
		/// 使用二分法实现LastOrDefault()的选值(假设集合已排序)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="X"></typeparam>
		/// <param name="source"></param>
		/// <param name="value"></param>
		/// <param name="keySelect"></param>
		/// <returns></returns>
		public static T LastOrDefaultByBinarySearch<T, X>(this IList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
		{
			var idx = source.LastOrDefaultIndexByBinarySearch(value, keySelect);
			return source[idx];
		}

		public static int LastOrDefaultIndexByBinarySearch<T, X>(this IList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
		{
			var idx = source.BinarySearchBy(value, keySelect);
			var i = Math.Max(0, idx < 0 ? ((~idx) - 1) : idx);
			return i;
		}

		public static T LastOrDefaultByBinarySearch<T, X>(this IReadOnlyList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
		{
			var idx = source.BinarySearchBy(value, keySelect);
			var i = Math.Max(0, idx < 0 ? ((~idx) - 1) : idx);
			return source[i];
		}

		public static int InsertBySortBy<T, X>(this IList<T> insertable, T needInsert, Func<T, X> keySelect)
		{
			var comparer = Comparer<X>.Default;
			var needInsertKey = keySelect(needInsert);

			for (int i = 0; i < insertable.Count; i++)
			{
				var cmp = comparer.Compare(needInsertKey, keySelect(insertable[i]));
				if (cmp < 0)
				{
					insertable.Insert(i, needInsert);
					return i;
				}
			}

			insertable.Add(needInsert);
			return insertable.Count - 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T min, T max) MaxMinBy<T>(this IEnumerable<T> collection)
			=> collection.MaxMinBy(x => x, Comparer<T>.Default.Compare);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T min, T max) MaxMinBy<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector = default) where X : IComparable<X>
			=> collection.MaxMinBy((a, b) => keySelector(a).CompareTo(keySelector(b)));
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T min, T max) MaxMinBy<T>(this IEnumerable<T> collection, Func<T, T, int> comparer = default)
			=> collection.MaxMinBy(x => x, comparer);
		public static (X min, X max) MaxMinBy<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector, Func<X, X, int> comparer)
		{
			var first = collection.FirstOrDefault();
			X min = keySelector(first);
			X max = keySelector(first);

			foreach (var item in collection)
			{
				var v = keySelector(item);
				if (comparer(min, v) > 0)
					min = v;
				if (comparer(v, max) > 0)
					max = v;
			}

			return (min, max);
		}

		/// <summary>
		/// 将集合分成一组组的子集合
		/// [1,2,3,4,5,6,7,8] --3--> [1,2,3],[4,5,6],[7,8]
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="wrapCount">子集合最大数量</param>
		/// <returns></returns>
		public static IEnumerable<IEnumerable<T>> SequenceWrap<T>(this IEnumerable<T> collection, int wrapCount)
		{
			var i = 0;

			var arr = ArrayPool<T>.Shared.Rent(wrapCount);

			foreach (var item in collection)
			{
				arr[i++] = item;

				if (i == wrapCount)
				{
					yield return arr.Take(wrapCount);
					i = 0;
				}
			}

			if (i != 0)
				yield return arr.Take(i).ToArray();

			ArrayPool<T>.Shared.Return(arr);
		}

		/// <summary>
		/// 将集合分成一组组的子集合,但子集合是连续的
		/// [1,2,3,4,5] --3--> [1,2,3],[2,3,4],[3,4,5]
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="collection"></param>
		/// <param name="wrapCount">子集合最大数量</param>
		/// <returns></returns>
		public static IEnumerable<IEnumerable<T>> SequenceConsecutivelyWrap<T>(this IEnumerable<T> collection, int wrapCount)
		{
			var link = new LinkedList<T>();

			foreach (var item in collection)
			{
				if (link.Count == wrapCount)
					link.RemoveFirst();

				link.AddLast(item);

				if (link.Count == wrapCount)
					yield return link.Take(wrapCount);
			}

			if (link.Count < wrapCount)
				yield return link.Take(wrapCount);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> UnionBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
			=> a.Union(b, new EqualsComparerWrapper<T>((x, y) =>
			{
				var kx = keySelector(x);
				var ky = keySelector(y);
				return (kx?.Equals(ky) ?? false) && (ky?.Equals(kx) ?? false);
			}));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> UnionBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
			=> a.Union(b, new EqualsComparerWrapper<T>(compFunc));

		public static IEnumerable<T> ExceptBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
			=> a.Union(b, new EqualsComparerWrapper<T>((x, y) =>
			{
				var kx = keySelector(x);
				var ky = keySelector(y);
				return (kx?.Equals(ky) ?? false) && (ky?.Equals(kx) ?? false);
			}));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<T> ExceptBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
			=> a.Except(b, new EqualsComparerWrapper<T>(compFunc));

		public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> compFunc, T defaultValue)
		{
			var val = a.FirstOrDefault(compFunc);
			val ??= defaultValue;
			return val;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<IEnumerable<T>> SplitByTurningGradient<T>(this IEnumerable<T> a, Func<T, float> valMapFunc) =>
			MathUtils.SplitByTurningGradient(a, valMapFunc);

		/// <summary>
		/// 判断集合是否只有一个元素
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="a"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsOnlyOne<T>(this IEnumerable<T> a)
			=> IsOnlyOne(a, out _);

		/// <summary>
		/// 判断集合是否只有一个
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="a"></param>
		/// <param name="firstElement"></param>
		/// <returns></returns>
		public static bool IsOnlyOne<T>(this IEnumerable<T> a, out T firstElement)
		{
			firstElement = default;

			var itor = a.GetEnumerator();
			if (!itor.MoveNext())
				return false;
			firstElement = itor.Current;
			return !itor.MoveNext();
		}

		/// <summary>
		/// 判断集合是否至少有一定数量的元素
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="a"></param>
		/// <param name="minCount"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AtLeastCount<T>(this IEnumerable<T> a, int minCount)
		{
			return a.Take(minCount).Count() == minCount;
		}

		/// <summary>
		/// 判断集合数量是否刚好
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="a"></param>
		/// <param name="minCount"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AtCount<T>(this IEnumerable<T> a, int minCount)
		{
			var i = 0;
			var itor = a.GetEnumerator();

			while (itor.MoveNext())
			{
				i++;
				if (i > minCount)
					return false;
			}

			return minCount == i;
		}

		/// <summary>
		/// 判断集合是否为空
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="a"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty<T>(this IEnumerable<T> a)
		{
			return !a.Any();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty<T>(this IEnumerable<T> a, Predicate<T> predicate) => a.Where(x => predicate(x)).IsEmpty();

		public delegate OUT IntervalByProcFunc<IN, OUT>(IN prev, IN cur);
		/// <summary>
		/// e.g IntervalBy([1,5,10,20] , (a,b)=>b-a) return [4,5,10]
		/// </summary>
		/// <typeparam name="IN"></typeparam>
		/// <typeparam name="OUT"></typeparam>
		/// <param name="list"></param>
		/// <param name="proc"></param>
		/// <returns></returns>
		public static IEnumerable<OUT> IntervalBy<IN, OUT>(this IEnumerable<IN> list, IntervalByProcFunc<IN, OUT> proc)
		{
			var itor = list.GetEnumerator();
			if (!itor.MoveNext())
				yield break;
			var prev = itor.Current;
			while (itor.MoveNext())
			{
				var cur = itor.Current;
				yield return proc(prev, cur);
				prev = cur;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<(T, int)> WithIndex<T>(this IEnumerable<T> list)
			=> list.Select((a, b) => (a, b));

		public static IEnumerable<T> IntersectMany<T, KEY>(this IEnumerable<IEnumerable<T>> list)
		{
			var itor = list.GetEnumerator();
			if (!itor.MoveNext())
				return Enumerable.Empty<T>();
			var cur = itor.Current;
			while (itor.MoveNext())
				cur = cur.Intersect(itor.Current);
			return cur;
		}

		public static IEnumerable<T> IntersectManyBy<T, KEY>(this IEnumerable<IEnumerable<T>> list, Func<T, KEY> keySelector)
		{
			var itor = list.GetEnumerator();
			if (!itor.MoveNext())
				return Enumerable.Empty<T>();

			var cur = itor.Current;
			while (itor.MoveNext())
				cur = cur.IntersectBy(itor.Current.Select(keySelector), keySelector);
			return cur;
		}
	}
}
