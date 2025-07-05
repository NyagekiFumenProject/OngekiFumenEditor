namespace OngekiFumenEditor.Utils
{
    public static class ObjectExtensionMethod
    {
        public static void ReturnToObjectPool<T>(this T obj) where T : new() => ObjectPool.ObjectPool<T>.Return(obj);
    }
}
