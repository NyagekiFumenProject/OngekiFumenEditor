using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    internal static class ObjectPoolRuntime
    {
        public static Action<ObjectPoolBase> RegisterAction { get; set; }
    }

    public class ObjectPool<T> : ObjectPoolBase where T : new()
    {
        #region AutoImpl

        private static ObjectPool<T> instance;
        private static ObjectPool<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ObjectPool<T>();
                    ObjectPoolRuntime.RegisterAction?.Invoke(instance);
                }

                return instance;
            }
        }

        public static void SetRegisterAction(Action<ObjectPoolBase> action)
        {
            ObjectPoolRuntime.RegisterAction = action;
        }

        #endregion

        private readonly ConcurrentBag<T> cacheObj = new();

        public static bool EnableTrim { get; set; } = true;
        public override int CachingObjectCount => cacheObj.Count;

#if DEBUG
        private readonly ConcurrentDictionary<int, (DateTime, string)> rentTimeMap = new();

        protected override void OnCheckIfLongTimeNotReturn()
        {
            foreach (var kvp in rentTimeMap)
            {
                var (time, mark) = kvp.Value;
                if ((DateTime.UtcNow - time).TotalSeconds > 5)
                {
                    throw new Exception($"pooled object which is marked \"{mark}\" has been rented for more than 10 seconds. Please check if it has been returned properly.");
                }
            }
        }
#endif

        protected override void OnReduceObjects()
        {
            if (!EnableTrim)
                return;

            var cachingObjectCount = CachingObjectCount;
            var count = cachingObjectCount > MaxTempCache ?
                (MaxTempCache + ((cachingObjectCount - MaxTempCache) / 2)) :
                cachingObjectCount / 4;

            for (var i = 0; i < count / 2; i++)
            {
                if (!cacheObj.TryTake(out _))
                    break;
            }
        }

        #region Sugar~

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
        public static bool Get(out T obj) => Instance.GetInternal(out obj);

#if DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Get(string rentMark, out T obj) => Instance.GetInternal(rentMark, out obj);

        private bool GetInternal(string rentMark, out T obj)
        {
            var isNewObject = GetInternal(out obj);

            if (isNewObject && !string.IsNullOrWhiteSpace(rentMark))
                rentTimeMap[obj.GetHashCode()] = (DateTime.UtcNow, rentMark);

            return isNewObject;
        }
#endif

        private bool GetInternal(out T obj)
        {
            var isSuccess = cacheObj.TryTake(out obj);
            if (isSuccess)
                (obj as ICacheCleanable)?.OnBeforeGetClean();
            else
                obj = new T();

            return !isSuccess;
        }

#if DEBUG
        public static T Get(string rentMark)
        {
            Get(rentMark, out var t);
            return t;
        }
#endif

        public static T Get()
        {
            Get(out var t);
            return t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(T obj) => Instance.ReturnInternal(obj);

        private void ReturnInternal(T obj)
        {
            if (obj == null)
                return;

            cacheObj.Add(obj);
            (obj as ICacheCleanable)?.OnAfterPutClean();

#if DEBUG
            rentTimeMap.TryRemove(obj.GetHashCode(), out _);
#endif
        }

        #endregion
    }

    public static class ObjectPool
    {
        public static void SetRegisterAction(Action<ObjectPoolBase> action)
            => ObjectPool<object>.SetRegisterAction(action);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return<T>(T obj) where T : new()
            => ObjectPool<T>.Return(obj);

#if DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>(string rentMark) where T : new()
            => ObjectPool<T>.Get(rentMark);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Get<T>() where T : new()
            => ObjectPool<T>.Get();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDisposable GetWithUsingDisposable<T>(out T obj, out bool isNewObject) where T : new()
            => ObjectPool<T>.GetWithUsingDisposable(out obj, out isNewObject);
    }
}

