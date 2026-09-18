using Caliburn.Micro;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
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
        private const int SchedulerScanDelayMs = 5;

        private AbortableThread runThread;

        private readonly ConcurrentDictionary<string, SchedulerEntry> schedulers = new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<ISchedulable, byte> runningSchedulers = new();
        private readonly ConcurrentDictionary<Task, byte> runningTasks = new();
        private int initialized;

        public IEnumerable<ISchedulable> Schedulers => schedulers.Values.Select(x => x.Scheduler);

        public Task Init()
        {
            foreach (var s in IoC.GetAll<ISchedulable>())
                AddScheduler(s);

            if (Interlocked.Exchange(ref initialized, 1) == 1)
                return Task.CompletedTask;

            runThread = new AbortableThread(Run);
            runThread.Name = "SchedulerManager::Run()";
            runThread.Start();

            return Task.CompletedTask;
        }

        public Task AddScheduler(ISchedulable s)
        {
            if (s is null || string.IsNullOrEmpty(s.SchedulerName))
            {
                Log.LogWarn($"Can't add scheduler : {s?.SchedulerName} is null/exist.");
                return Task.CompletedTask;
            }

            if (!schedulers.TryAdd(s.SchedulerName, new SchedulerEntry(s)))
            {
                Log.LogWarn($"Can't add scheduler : {s.SchedulerName} is null/exist.");
                return Task.CompletedTask;
            }

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
            using var snapshot = ObjectPool.GetPooledList<SchedulerEntry>();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    snapshot.Clear();
                    var nowTs = Stopwatch.GetTimestamp();

                    foreach (var entry in schedulers.Values)
                        snapshot.Add(entry);

                    foreach (var entry in snapshot)
                    {
                        if (!ShouldInvoke(entry, nowTs))
                            continue;

                        QueueInvoke(entry, cancellationToken);
                    }

                    await Task.Delay(SchedulerScanDelayMs, cancellationToken).ConfigureAwait(false);
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

        private bool ShouldInvoke(SchedulerEntry entry, long nowTs)
        {
            if (Volatile.Read(ref entry.Removed) != 0)
                return false;

            var scheduler = entry.Scheduler;
            var lastCallTimestamp = Volatile.Read(ref entry.LastCallTimestamp);
            return lastCallTimestamp == 0 || Stopwatch.GetElapsedTime(lastCallTimestamp, nowTs) >= scheduler.ScheduleCallLoopInterval;
        }

        private void QueueInvoke(SchedulerEntry entry, CancellationToken cancellationToken)
        {
            if (!runningSchedulers.TryAdd(entry.Scheduler, 0))
                return;

            var task = Task.Factory.StartNew(
                static state =>
                {
                    var (manager, invokeEntry, token) = ((SchedulerManager manager, SchedulerEntry invokeEntry, CancellationToken token))state;
                    return manager.InvokeAndStamp(invokeEntry, token);
                },
                (this, entry, cancellationToken),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default).Unwrap();

            runningTasks.TryAdd(task, 0);
            _ = task.ContinueWith(
                static (completedTask, state) =>
                {
                    var manager = (SchedulerManager)state;
                    manager.runningTasks.TryRemove(completedTask, out _);
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task InvokeAndStamp(SchedulerEntry entry, CancellationToken cancellationToken)
        {
            try
            {
                if (Volatile.Read(ref entry.Removed) == 0 && !cancellationToken.IsCancellationRequested)
                    await entry.Scheduler.OnScheduleCall(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Log.LogError($"scheduler {entry.Scheduler.SchedulerName} throw exception:{e}", e);
            }
            finally
            {
                runningSchedulers.TryRemove(entry.Scheduler, out _);

                if (!cancellationToken.IsCancellationRequested &&
                    Volatile.Read(ref entry.Removed) == 0 &&
                    schedulers.TryGetValue(entry.Scheduler.SchedulerName, out var currentEntry) &&
                    ReferenceEquals(currentEntry, entry))
                {
                    Volatile.Write(ref entry.LastCallTimestamp, Stopwatch.GetTimestamp());
                }
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

            using var activeTasks = ObjectPool.GetPooledList<Task>();
            activeTasks.AddRange(runningTasks.Keys);
            if (activeTasks.Count > 0)
                await Task.WhenAll(activeTasks).ConfigureAwait(false);

            using var termTasks = ObjectPool.GetPooledList<Task>();
            foreach (var scheduler in schedulers.Values.Select(x => x.Scheduler))
            {
                Log.LogInfo("Call OnSchedulerTerm() :" + scheduler.SchedulerName);
                termTasks.Add(Task.Factory.StartNew(
                    static state => ((ISchedulable)state).OnSchedulerTerm(),
                    scheduler,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default));
            }

            if (termTasks.Count > 0)
                await Task.WhenAll(termTasks).ConfigureAwait(false);

            schedulers.Clear();
        }

        public Task RemoveScheduler(ISchedulable s)
        {
            if (s is null || string.IsNullOrEmpty(s.SchedulerName))
            {
                Log.LogWarn($"Can't remove scheduler : {s?.SchedulerName} is null or not exist.");
                return Task.CompletedTask;
            }

            if (!schedulers.TryRemove(s.SchedulerName, out var removedEntry))
            {
                Log.LogWarn($"Can't remove scheduler : {s.SchedulerName} is null or not exist.");
                return Task.CompletedTask;
            }

            Volatile.Write(ref removedEntry.Removed, 1);
            Log.LogDebug("Remove scheduler: " + s.SchedulerName);

            return Task.CompletedTask;
        }

        private sealed class SchedulerEntry
        {
            public SchedulerEntry(ISchedulable scheduler)
            {
                Scheduler = scheduler;
            }

            public ISchedulable Scheduler { get; }
            public long LastCallTimestamp;
            public int Removed;
        }
    }
}
