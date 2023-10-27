namespace OngekiFumenEditor.Utils.ObjectPool
{
	public abstract class ObjectPoolBase
	{
		public abstract int CachingObjectCount { get; }
		public static int MaxTempCache { get; set; } = 10;

		internal void OnPreReduceSchedule()
		{
			var before = CachingObjectCount;
			OnReduceObjects();
			var after = CachingObjectCount;

			var diff = after - before;

			if (diff < 0)
				Log.LogDebug($"Reduced {diff} {GetType().GetTypeName()} objects");

		}

		protected abstract void OnReduceObjects();
	}
}
