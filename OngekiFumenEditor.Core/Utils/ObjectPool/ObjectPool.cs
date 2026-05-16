using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    public class ObjectPool<T> where T : class, new()
    {
        private static readonly Microsoft.Extensions.ObjectPool.ObjectPool<T> pool =
            new DefaultObjectPoolProvider().Create<T>();

        private class AutoDisposable : IDisposable
        {
            public T RefObject { get; set; }

            public void Dispose()
            {
                if (RefObject is not null)
                    Return(RefObject);

                RefObject = default;
                ObjectPool<AutoDisposable>.Return(this);
            }
        }

        public static IDisposable GetWithUsingDisposable(out T obj, out bool isNewObject)
        {
            isNewObject = Get(out obj);
            var d = ObjectPool<AutoDisposable>.Get();
            d.RefObject = obj;
            return d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Get(out T obj)
        {
            obj = pool.Get();
            return false;
        }

#if DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Get(string rentMark, out T obj) => Get(out obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get(string rentMark) => Get();
#endif

        public static T Get()
        {
            Get(out var t);
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T obj)
        {
            if (obj == null)
                return;

            pool.Return(obj);
        }
    }

    public static class ObjectPool
    {
        [Obsolete("Object pool registration is no longer used.")]
        public static void SetRegisterAction(Action<object> action)
        {
        }

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
        public static IDisposable GetWithUsingDisposable<T>(out T obj, out bool isNewObject) where T : class, new()
            => ObjectPool<T>.GetWithUsingDisposable(out obj, out isNewObject);
    }
}
