using OngekiFumenEditor.Core.Base.Collections.Base;
using OngekiFumenEditor.Core.Utils.ObjectPool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils
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
            if (list.Length >= idx + 1)
            {
                element = list[idx];
                return true;
            }

            element = default;
            return false;
        }

        public static void DistinctSelf<T>(this ICollection<T> collection)
        {
            using var d = collection.Except(collection.Distinct()).ToListWithObjectPool(out var removes);
            foreach (var rm in removes)
                collection.Remove(rm);
        }

        public static void DistinctBySelf<T, Y>(this ICollection<T> collection, Func<T, Y> keySelect)
        {
            using var d = collection.Except(DistinctByCompat(collection, keySelect)).ToListWithObjectPool(out var removes);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> DistinctContinuousBy<T, Y>(this IEnumerable<T> collection, Func<T, Y> keySelect)
            => collection.DistinctContinuousBy((a, b) => Equals(keySelect(a), keySelect(b)));

        public static int FirstIndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            int index = 0;

            foreach (var item in source)
            {
                if (predicate(item))
                    return index;

                index++;
            }

            return -1;
        }

        public static IEnumerable<T> DistinctContinuousBy<T>(this IEnumerable<T> collection, Func<T, T, bool> compFunc)
        {
            using var itor = collection.GetEnumerator();
            var isFirst = true;
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
                {
                    yield return value;
                }

                prev = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> FilterNull<T>(this IEnumerable<T> collection) => collection.Where(x => x != null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> FilterNullBy<T, X>(this IEnumerable<T> collection, Func<T, X> keySelector) => collection.Where(x => keySelector(x) != null);

        public static T FindNextOrDefault<T>(this IEnumerable<T> collection, T taget)
        {
            using var itor = collection.GetEnumerator();
            var prev = default(T);

            while (itor.MoveNext())
            {
                if (Equals(taget, prev))
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
            using var itor = collection.GetEnumerator();
            if (!itor.MoveNext())
                return default;

            var first = itor.Current;
            var min = keySelector(first);
            var minValue = first;

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
            using var itor = collection.GetEnumerator();
            if (!itor.MoveNext())
                return default;

            var first = itor.Current;
            var max = keySelector(first);
            var maxValue = first;

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
            using var itor = collection.GetEnumerator();
            var prev = default(T);

            while (itor.MoveNext())
            {
                if (Equals(itor.Current, taget))
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
            var hi = insertable.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var val = keySelect(insertable[i]);
                var order = val.CompareTo(value);

                if (order == 0)
                {
                    for (var r = i + 1; r < insertable.Count; r++)
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
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return ~lo;
        }

        private static int BinarySearchBy<T, X>(this IReadOnlyList<T> insertable, X value, Func<T, X> keySelect, int lo = 0) where X : IComparable<X>
        {
            var hi = insertable.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var val = keySelect(insertable[i]);
                var order = val.CompareTo(value);

                if (order == 0)
                {
                    for (var r = i + 1; r < insertable.Count; r++)
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
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return ~lo;
        }

        public static T LastOrDefaultByBinarySearch<T, X>(this IList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
        {
            var idx = source.LastOrDefaultIndexByBinarySearch(value, keySelect);
            return source[idx];
        }

        public static int LastOrDefaultIndexByBinarySearch<T, X>(this IList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
        {
            var idx = source.BinarySearchBy(value, keySelect);
            return Math.Max(0, idx < 0 ? (~idx) - 1 : idx);
        }

        public static T LastOrDefaultByBinarySearch<T, X>(this IReadOnlyList<T> source, X value, Func<T, X> keySelect) where X : IComparable<X>
        {
            var idx = source.BinarySearchBy(value, keySelect);
            var i = Math.Max(0, idx < 0 ? (~idx) - 1 : idx);
            return source[i];
        }

        public static int InsertBySortBy<T, X>(this IList<T> insertable, T needInsert, Func<T, X> keySelect)
        {
            var comparer = Comparer<X>.Default;
            var needInsertKey = keySelect(needInsert);

            for (var i = 0; i < insertable.Count; i++)
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
            var min = keySelector(first);
            var max = keySelector(first);

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

        public static IEnumerable<IEnumerable<T>> SequenceWrap<T>(this IEnumerable<T> collection, int wrapCount)
        {
            var i = 0;
            var arr = new T[wrapCount];

            foreach (var item in collection)
            {
                arr[i++] = item;

                if (i == wrapCount)
                {
                    yield return arr;
                    i = 0;
                }
            }

            if (i != 0)
                yield return arr.Take(i).ToArray();
        }

        public static IEnumerable<T[]> SequenceConsecutivelyWrap<T>(this IEnumerable<T> collection, int wrapCount)
        {
#if DEBUG
            if (wrapCount < 2)
                throw new ArgumentException($"wrapCount({wrapCount}) must be >= 2");
#endif

            var arr = new T[wrapCount];
            using var itor = collection.GetEnumerator();

            for (var i = 0; i < wrapCount; i++)
            {
                if (!itor.MoveNext())
                    yield break;
                arr[i] = itor.Current;
            }

            yield return arr;

            while (itor.MoveNext())
            {
                Array.Copy(arr, 1, arr, 0, wrapCount - 1);
                arr[wrapCount - 1] = itor.Current;
                yield return arr;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> UnionBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
            => a.Union(b, BuildEqualsComparer(keySelector));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> UnionBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
            => a.Union(b, new EqualsComparerWrapper<T>(compFunc));

        public static IEnumerable<T> ExceptBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
            => a.Except(b, BuildEqualsComparer(keySelector));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<T> ExceptBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
            => a.Except(b, new EqualsComparerWrapper<T>(compFunc));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<IEnumerable<T>> SplitByTurningGradient<T>(this IEnumerable<T> a, Func<T, float> valMapFunc)
        {
            static float CalcGradient(float a, float b)
            {
                if (a == b)
                    return float.MaxValue;

                return -(a - b);
            }

            using var itor = a.GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var list = new List<T>();
            var prevPoint = itor.Current;
            var prevSign = 0;

            while (true)
            {
                if (!itor.MoveNext())
                    break;

                var point = itor.Current;
                var sign = MathF.Sign(CalcGradient(valMapFunc(prevPoint), valMapFunc(point)));

                if (prevSign != sign && list.Count != 0)
                {
                    yield return list;
                    list = new List<T> { prevPoint };
                }

                prevPoint = point;
                prevSign = sign;
                list.Add(point);
            }

            if (list.Count > 0)
                yield return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnlyOne<T>(this IEnumerable<T> a)
            => IsOnlyOne(a, out _);

        public static bool IsOnlyOne<T>(this IEnumerable<T> a, out T firstElement)
        {
            firstElement = default;

            using var itor = a.GetEnumerator();
            if (!itor.MoveNext())
                return false;

            firstElement = itor.Current;
            return !itor.MoveNext();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AtLeastCount<T>(this IEnumerable<T> a, int minCount)
            => a.Take(minCount).Count() == minCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AtCount<T>(this IEnumerable<T> a, int minCount)
        {
            var i = 0;
            using var itor = a.GetEnumerator();

            while (itor.MoveNext())
            {
                i++;
                if (i > minCount)
                    return false;
            }

            return minCount == i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty<T>(this IEnumerable<T> source)
        {
            if (source is ICollection<T> collectionOfT)
                return collectionOfT.Count == 0;

            if (source is ICollection collection)
                return collection.Count == 0;

            if (source is IReadOnlyCollection<T> readOnlyCollection)
                return readOnlyCollection.Count == 0;

            return !source.Any();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty<T>(this IEnumerable<T> a, Predicate<T> predicate) => a.Where(x => predicate(x)).IsEmpty();

        public delegate TOut IntervalByProcFunc<TIn, TOut>(TIn prev, TIn cur);

        public static IEnumerable<TOut> IntervalBy<TIn, TOut>(this IEnumerable<TIn> list, IntervalByProcFunc<TIn, TOut> proc)
        {
            using var itor = list.GetEnumerator();
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

        public static IEnumerable<T> IntersectMany<T, TKey>(this IEnumerable<IEnumerable<T>> list)
        {
            using var itor = list.GetEnumerator();
            if (!itor.MoveNext())
                return Enumerable.Empty<T>();

            var cur = itor.Current;
            while (itor.MoveNext())
                cur = cur.Intersect(itor.Current);

            return cur;
        }

        public static IEnumerable<T> IntersectManyBy<T, TKey>(this IEnumerable<IEnumerable<T>> list, Func<T, TKey> keySelector)
        {
            using var itor = list.GetEnumerator();
            if (!itor.MoveNext())
                return Enumerable.Empty<T>();

            var cur = itor.Current;
            while (itor.MoveNext())
                cur = IntersectByCompat(cur, itor.Current.Select(keySelector), keySelector);

            return cur;
        }

        private static IEnumerable<T> DistinctByCompat<T, TKey>(IEnumerable<T> collection, Func<T, TKey> keySelector)
        {
            var set = new HashSet<TKey>();
            foreach (var item in collection)
            {
                if (set.Add(keySelector(item)))
                    yield return item;
            }
        }

        private static IEnumerable<T> IntersectByCompat<T, TKey>(IEnumerable<T> source, IEnumerable<TKey> keys, Func<T, TKey> keySelector)
        {
            var set = new HashSet<TKey>(keys);
            foreach (var item in source)
            {
                if (set.Remove(keySelector(item)))
                    yield return item;
            }
        }

        private static EqualsComparerWrapper<T> BuildEqualsComparer<T, TKey>(Func<T, TKey> keySelector)
        {
            return new EqualsComparerWrapper<T>((x, y) => Equals(keySelector(x), keySelector(y)));
        }
    }
}

