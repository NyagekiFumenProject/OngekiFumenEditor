using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class LinqExtensionMethod
    {
        public static void ForEach<T>(this IEnumerable<T> list, Action<T> fun)
        {
            if (list is null)
                return;
            foreach (var item in list)
                fun(item);
        }

        public static void RemoveRange<T>(this Collection<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Remove(x));

        public static void RemoveRange<T>(this HashSet<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Remove(x));

        public static void AddRange<T>(this Collection<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Add(x));

        public static void AddRange<T>(this HashSet<T> collection, IEnumerable<T> source) => source.ForEach(x => collection.Add(x));

        public static IEnumerable<T> DistinctBy<T, Y>(this IEnumerable<T> collection, Func<T, Y> keySelect) => collection.DistinctBy((a, b) => keySelect(a)?.Equals(keySelect(b)) ?? keySelect(b)?.Equals(keySelect(a)) ?? true);

        public static IEnumerable<T> DistinctBy<T>(this IEnumerable<T> collection, Func<T, T, bool> compFunc) => collection.Distinct(new FuncDistinctComparer<T>(compFunc));

        public static IEnumerable<T> FilterNull<T>(this IEnumerable<T> collection) => collection.Where(x => x != null);

        public static T FindNextOrDefault<T>(this IEnumerable<T> collection, T taget)
        {
            var itor = collection.GetEnumerator();
            var prev = default(T);

            while (itor.MoveNext())
            {
                if (prev.Equals(taget))
                    return itor.Current;
                prev = itor.Current;
            }

            return default;
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

        public static void Sort<T>(this List<T> list, Func<T, T, int> compFunc) => list.Sort(new ComparerWrapper<T>(compFunc));

        public static void SortBy<T, X>(this List<T> list, Func<T, X> keySelect) => list.Sort((a, b) => Comparer<X>.Default.Compare(keySelect(a), keySelect(b)));

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

        public static (T min, T max) MaxMinBy<T>(this IEnumerable<T> collection)
            => collection.MaxMinBy(x => x, (a, b) => Comparer<T>.Default.Compare(a, b));
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

        public static IEnumerable<T> UnionBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
            => a.Union(b, new EqualsComparerWrapper<T>((x, y) =>
            {
                var kx = keySelector(x);
                var ky = keySelector(y);
                return (kx?.Equals(ky) ?? false) && (ky?.Equals(kx) ?? false);
            }));

        public static IEnumerable<T> UnionBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
            => a.Union(b, new EqualsComparerWrapper<T>(compFunc));

        public static IEnumerable<T> ExceptBy<T, X>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, X> keySelector)
            => a.Union(b, new EqualsComparerWrapper<T>((x, y) =>
            {
                var kx = keySelector(x);
                var ky = keySelector(y);
                return (kx?.Equals(ky) ?? false) && (ky?.Equals(kx) ?? false);
            }));

        public static IEnumerable<T> ExceptBy<T>(this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, bool> compFunc)
            => a.Except(b, new EqualsComparerWrapper<T>(compFunc));


        public static T FirstOrDefault<T>(this IEnumerable<T> a, Func<T, bool> compFunc, T defaultValue)
        {
            var val = a.FirstOrDefault(compFunc);
            val ??= defaultValue;
            return val;
        }
    }
}
