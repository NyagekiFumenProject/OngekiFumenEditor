using OngekiFumenEditor.Utils.ObjectPool;
using System;
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
    }
}
