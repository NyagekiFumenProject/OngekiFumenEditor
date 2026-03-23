namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    public abstract class ObjectPoolBase
    {
        public abstract int CachingObjectCount { get; }
        public static int MaxTempCache { get; set; } = 10;

        public void OnPreReduceSchedule()
        {
            var before = CachingObjectCount;
            OnReduceObjects();
#if DEBUG
            OnCheckIfLongTimeNotReturn();
#endif
            var after = CachingObjectCount;

            var diff = before - after;

            if (diff > 0)
                CoreLog.LogDebug($"Reduced {diff} {GetType().GetTypeName()} objects");
        }

        protected abstract void OnReduceObjects();
#if DEBUG
        protected abstract void OnCheckIfLongTimeNotReturn();
#endif
    }
}

