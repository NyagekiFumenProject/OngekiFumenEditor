namespace OngekiFumenEditor.Core.Utils
{
    public static class ObjectExtensionMethod
    {
        public static void ReturnToObjectPool<T>(this T obj) where T : class, new() => ObjectPool.ObjectPool<T>.Return(obj);
    }
}

