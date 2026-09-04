using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Utils.ObjectPool
{
    public class ObjectPool<T> where T : class, new()
    {
        private static readonly Microsoft.Extensions.ObjectPool.ObjectPool<T> pool =
            new DefaultObjectPoolProvider().Create<T>();

        private sealed class AutoDisposable : IDisposable
        {
            private T refObject;
            private bool isActive;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Rent(T value)
            {
                refObject = value;
                isActive = true;
            }

            public void Dispose()
            {
                if (!isActive)
                    return;

                isActive = false;
                var value = refObject;
                refObject = default;
                Return(value);
                ObjectPool<AutoDisposable>.Return(this);
            }
        }

        public static IDisposable GetWithUsingDisposable(out T obj)
        {
            Get(out obj);
            var disposable = ObjectPool<AutoDisposable>.Get();
            disposable.Rent(obj);
            return disposable;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Get(out T obj)
        {
            obj = pool.Get();
        }

#if DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Get(string rentMark, out T obj) => Get(out obj);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get(string rentMark) => Get();
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get()
        {
            Get(out var value);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T obj)
        {
            if (obj is null)
                return;

            pool.Return(obj);
        }
    }
}
