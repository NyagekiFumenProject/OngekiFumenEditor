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
		public string SchedulerName => "Object Pool Maintenance Scheduler";

		public TimeSpan ScheduleCallLoopInterval { get; } = TimeSpan.FromSeconds(10);

		HashSet<ObjectPoolBase> object_pools = new HashSet<ObjectPoolBase>();

		public void RegisterNewObjectPool(ObjectPoolBase pool)
		{
			if (pool == null)
				return;

			lock (this)
			{
				object_pools.Add(pool);
                Log.LogDebug($"Register new object pool :{pool.GetType().GetTypeName()}");
            }
		}

		public void OnSchedulerTerm()
		{

		}

		public Task OnScheduleCall(CancellationToken cancellationToken)
		{
			lock (this)
			{
				foreach (var pool in object_pools)
					pool.OnPreReduceSchedule();

				return Task.CompletedTask;
			}
		}
	}
}

