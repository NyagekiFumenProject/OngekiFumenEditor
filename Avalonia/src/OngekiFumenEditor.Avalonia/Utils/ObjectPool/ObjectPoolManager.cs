using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;

namespace OngekiFumenEditor.Avalonia.Utils.ObjectPool
{
	[Export(typeof(ISchedulable))]
	[Export(typeof(ObjectPoolManager))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	[RegisterSingleton<ObjectPoolManager>]
	public class ObjectPoolManager : ISchedulable
	{
		private static readonly object RegistryLock = new();
		private static readonly HashSet<ObjectPoolBase> ObjectPools = new();

		public string SchedulerName => "Object Pool Maintenance Scheduler";

		public TimeSpan ScheduleCallLoopInterval { get; } = TimeSpan.FromSeconds(10);

		public void RegisterNewObjectPool(ObjectPoolBase pool)
			=> RegisterObjectPool(pool);

		internal static void RegisterObjectPool(ObjectPoolBase pool)
		{
			if (pool == null)
				return;

			lock (RegistryLock)
			{
				ObjectPools.Add(pool);
                Log.LogDebug($"Register new object pool :{pool.GetType().GetTypeName()}");
            }
		}

		public void OnSchedulerTerm()
		{

		}

		public Task OnScheduleCall(CancellationToken cancellationToken)
		{
			lock (RegistryLock)
			{
				foreach (var pool in ObjectPools)
					pool.OnPreReduceSchedule();

				return Task.CompletedTask;
			}
		}
	}
}

