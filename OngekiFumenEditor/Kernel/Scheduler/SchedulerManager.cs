using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Scheduler
{
	[Export(typeof(ISchedulerManager))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	internal class SchedulerManager : ISchedulerManager
	{
		private AbortableThread runThread;

		private List<ISchedulable> schedulers { get; } = new List<ISchedulable>();

		private ConcurrentDictionary<ISchedulable, DateTime> schedulersCallTime { get; } = new();

		public IEnumerable<ISchedulable> Schedulers => schedulers;

		public Task Init()
		{
			foreach (var s in IoC.GetAll<ISchedulable>())
				AddScheduler(s);

			runThread = new AbortableThread(Run);
			runThread.Name = "SchedulerManager::Run()";
			runThread.Start();

			return Task.CompletedTask;
		}

		public Task AddScheduler(ISchedulable s)
		{
			if (s is null || schedulers.FirstOrDefault(x => x.SchedulerName.Equals(s.SchedulerName)) != null)
			{
				Log.LogWarn($"Can't add scheduler : {s?.SchedulerName} is null/exist.");
				return Task.CompletedTask;
			}

			schedulers.Add(s);
			schedulersCallTime[s] = DateTime.MinValue;
			Log.LogDebug("Added new scheduler: " + s.SchedulerName);

			return Task.CompletedTask;
		}

		private async void Run(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				var schedulers = Schedulers
					.Where(x => x is not null && DateTime.Now - schedulersCallTime[x] >= x.ScheduleCallLoopInterval)
					.Select(x => x.OnScheduleCall(cancellationToken).ContinueWith(_ => schedulersCallTime[x] = DateTime.Now))
					.ToArray();
				if (schedulers.Length > 0)
					await Task.WhenAll(schedulers);
				else
					await Task.Delay(10);
			}
		}

		public Task Term()
		{
			Log.LogDebug("call SchedulerManager.Dispose()");

			try
			{
				runThread.Abort();
			}
			catch { }

			foreach (var scheduler in Schedulers)
			{
				Log.LogInfo("Call OnSchedulerTerm() :" + scheduler.SchedulerName);
				scheduler.OnSchedulerTerm();
			}

			return Task.CompletedTask;
		}

		public Task RemoveScheduler(ISchedulable s)
		{
			if (s is null || schedulers.FirstOrDefault(x => x.SchedulerName.Equals(s.SchedulerName)) is null)
			{
				Log.LogWarn($"Can't remove scheduler : {s?.SchedulerName} is null or not exist.");
				return Task.CompletedTask;
			}

			schedulers.Remove(s);
			Log.LogDebug("Remove scheduler: " + s.SchedulerName);

			return Task.CompletedTask;
		}
	}
}
