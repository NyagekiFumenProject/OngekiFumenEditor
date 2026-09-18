using System;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Utils.ObjectPool
{
    public static class ObjectPool
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return<T>(T obj) where T : class, new()
            => ObjectPool<T>.Return(obj);

#if DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(string rentMark) where T : class, new()
            => ObjectPool<T>.Get(rentMark);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>() where T : class, new()
            => ObjectPool<T>.Get();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDisposable GetWithUsingDisposable<T>(out T obj) where T : class, new()
            => ObjectPool<T>.GetWithUsingDisposable(out obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPooledList<T> GetPooledList<T>()
        {
            var list = ObjectPool<PooledList<T>>.Get();
            list.Rent();
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPooledSet<T> GetPooledSet<T>()
        {
            var set = ObjectPool<PooledSet<T>>.Get();
            set.Rent();
            return set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IPooledDictionary<TKey, TValue> GetPooledDictionary<TKey, TValue>()
        {
            var dictionary = ObjectPool<PooledDictionary<TKey, TValue>>.Get();
            dictionary.Rent();
            return dictionary;
        }
    }
}
