using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
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

        private ConcurrentDictionary<ISchedulable, long> schedulersCallTime { get; } = new();

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
            schedulersCallTime[s] = 0L;
            Log.LogDebug("Added new scheduler: " + s.SchedulerName);

            return Task.CompletedTask;
        }

        private void Run(CancellationToken cancellationToken)
        {
            try
            {
                RunAsync(cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.LogError($"scheduler loop throw exception:{e}", e);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var pending = new List<Task>(16);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    pending.Clear();
                    var nowTs = Stopwatch.GetTimestamp();

                    foreach (var x in schedulers)
                    {
                        if (x is null)
                            continue;
                        var lastTs = schedulersCallTime[x];
                        if (Stopwatch.GetElapsedTime(lastTs, nowTs) < x.ScheduleCallLoopInterval)
                            continue;
                        pending.Add(InvokeAndStamp(x, cancellationToken));
                    }

                    if (pending.Count > 0)
                        await Task.WhenAll(pending);
                    else
                        await Task.Delay(10, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.LogError($"scheduler loop throw exception:{e}", e);
                }
            }
        }

        private async Task InvokeAndStamp(ISchedulable s, CancellationToken cancellationToken)
        {
            try
            {
                await s.OnScheduleCall(cancellationToken);
            }
            finally
            {
                schedulersCallTime[s] = Stopwatch.GetTimestamp();
            }
        }

        public async Task Term()
        {
            Log.LogDebug("call SchedulerManager.Dispose()");

            try
            {
                await runThread.AbortAsync();
            }
            catch { }

            foreach (var scheduler in Schedulers)
            {
                Log.LogInfo("Call OnSchedulerTerm() :" + scheduler.SchedulerName);
                scheduler.OnSchedulerTerm();
            }
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
