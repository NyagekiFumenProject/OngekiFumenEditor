using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

[RegisterSingleton<ISchedulerManager>]
internal class SchedulerManager : ISchedulerManager
{
    private const int SchedulerScanDelayMs = 5;

    private readonly ConcurrentDictionary<string, SchedulerEntry> schedulers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ISchedulable, byte> runningSchedulers = new();
    private readonly ConcurrentDictionary<Task, byte> runningTasks = new();
    private readonly object lifecycleGate = new();

    private CancellationTokenSource runCts;
    private Task runTask;
    private Task termTask = Task.CompletedTask;
    private bool isTerminating;

    public IEnumerable<ISchedulable> Schedulers =>
        schedulers.Values.Select(static entry => entry.Scheduler).ToArray();

    public Task Init()
    {
        // Initialization and shutdown must not publish competing run loops.
        lock (lifecycleGate)
        {
            if (isTerminating)
                throw new InvalidOperationException("Cannot initialize the scheduler during termination.");
            if (runTask is { IsCompleted: false })
                return Task.CompletedTask;

            foreach (var scheduler in IoC.GetAll<ISchedulable>())
                AddScheduler(scheduler);

            runCts?.Dispose();
            runCts = new CancellationTokenSource();
            var cancellationToken = runCts.Token;
            runTask = Task.Run(() => Run(cancellationToken), CancellationToken.None);

            UiLatencyDiag.Start(); // temporary latency diagnostics
        }

        return Task.CompletedTask;
    }

    public Task AddScheduler(ISchedulable scheduler)
    {
        if (string.IsNullOrEmpty(scheduler?.SchedulerName))
        {
            Log.LogWarning($"Can't add scheduler : {scheduler?.GetType()?.Name} is null/empty.");
            return Task.CompletedTask;
        }

        lock (lifecycleGate)
        {
            if (isTerminating)
            {
                Log.LogWarning($"Can't add scheduler during termination: {scheduler.SchedulerName}");
                return Task.CompletedTask;
            }
            if (!schedulers.TryAdd(scheduler.SchedulerName, new SchedulerEntry(scheduler)))
            {
                Log.LogWarning($"Can't add scheduler : {scheduler.SchedulerName} is already registered.");
                return Task.CompletedTask;
            }
        }

        Log.LogDebug("Added new scheduler: " + scheduler.SchedulerName);
        return Task.CompletedTask;
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var nowTimestamp = Stopwatch.GetTimestamp();
                    foreach (var pair in schedulers)
                        QueueInvoke(pair.Value, nowTimestamp, cancellationToken);

                    await Task.Delay(SchedulerScanDelayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Log.LogError($"scheduler loop throw exception:{exception}", exception);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Log.LogError($"scheduler loop throw exception:{exception}", exception);
        }
    }

    private static bool ShouldInvoke(SchedulerEntry entry, long nowTimestamp)
    {
        if (Volatile.Read(ref entry.Removed) != 0)
            return false;

        var lastCallTimestamp = Volatile.Read(ref entry.LastCallTimestamp);
        return lastCallTimestamp == 0 ||
               Stopwatch.GetElapsedTime(lastCallTimestamp, nowTimestamp) >= entry.Scheduler.ScheduleCallLoopInterval;
    }

    private void QueueInvoke(SchedulerEntry entry, long nowTimestamp, CancellationToken cancellationToken)
    {
        Task task;
        lock (lifecycleGate)
        {
            if (isTerminating || cancellationToken.IsCancellationRequested || !ShouldInvoke(entry, nowTimestamp))
                return;

            if (!runningSchedulers.TryAdd(entry.Scheduler, 0))
                return;

            UiLatencyDiag.CountSchedulerQueued(entry.Scheduler.SchedulerName); // temporary latency diagnostics

            try
            {
                task = Task.Factory.StartNew(
                    static state =>
                    {
                        var (manager, invokeEntry, token) =
                            ((SchedulerManager manager, SchedulerEntry invokeEntry, CancellationToken token))state;
                        return manager.InvokeAndStamp(invokeEntry, token);
                    },
                    (this, entry, cancellationToken),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default).Unwrap();
            }
            catch
            {
                runningSchedulers.TryRemove(entry.Scheduler, out _);
                throw;
            }

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
        catch (Exception exception)
        {
            Log.LogError($"scheduler {entry.Scheduler.SchedulerName} throw exception:{exception}", exception);
        }
        finally
        {
            lock (lifecycleGate)
            {
                if (!cancellationToken.IsCancellationRequested &&
                    Volatile.Read(ref entry.Removed) == 0 &&
                    schedulers.TryGetValue(entry.Scheduler.SchedulerName, out var currentEntry) &&
                    ReferenceEquals(currentEntry, entry))
                {
                    Volatile.Write(ref entry.LastCallTimestamp, Stopwatch.GetTimestamp());
                }
                // Publish the completion timestamp before permitting the next call.
                runningSchedulers.TryRemove(entry.Scheduler, out _);
            }
        }
    }

    public Task Term()
    {
        lock (lifecycleGate)
        {
            if (isTerminating)
                return termTask;

            isTerminating = true;
            var cancellationSource = runCts;
            var schedulerLoop = runTask;
            // Assign while holding the gate so every concurrent caller observes the same task.
            termTask = TermCore(cancellationSource, schedulerLoop);
            return termTask;
        }
    }

    private async Task TermCore(CancellationTokenSource cancellationSource, Task schedulerLoop)
    {
        Log.LogDebug("call SchedulerManager.Term()");
        try
        {
            if (cancellationSource is not null)
            {
                try
                {
                    await cancellationSource.CancelAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Log.LogError($"scheduler cancellation callback failed:{exception}", exception);
                }
            }
            if (schedulerLoop is not null)
                await schedulerLoop.ConfigureAwait(false);

            // The loop has stopped and QueueInvoke cannot add tasks during shutdown.
            // Include callbacks whose registration was removed while they were running.
            await Task.WhenAll(runningTasks.Keys).ConfigureAwait(false);

            var termTasks = new List<Task>();
            foreach (var entry in schedulers.Values)
                termTasks.Add(Task.Run(() => InvokeTerm(entry.Scheduler)));
            await Task.WhenAll(termTasks).ConfigureAwait(false);
        }
        finally
        {
            lock (lifecycleGate)
            {
                schedulers.Clear();
                runningTasks.Clear();
                cancellationSource?.Dispose();
                runCts = null;
                runTask = null;
                isTerminating = false;
            }
        }
    }

    private static void InvokeTerm(ISchedulable scheduler)
    {
        try
        {
            scheduler.OnSchedulerTerm();
        }
        catch (Exception exception)
        {
            Log.LogError($"scheduler {scheduler.SchedulerName} termination failed:{exception}", exception);
        }
    }

    public Task RemoveScheduler(ISchedulable scheduler)
    {
        if (scheduler is null || string.IsNullOrEmpty(scheduler.SchedulerName))
        {
            Log.LogWarning($"Can't remove scheduler : {scheduler?.SchedulerName} is null/empty.");
            return Task.CompletedTask;
        }

        lock (lifecycleGate)
        {
            if (!schedulers.TryRemove(scheduler.SchedulerName, out var removedEntry))
            {
                Log.LogWarning($"Can't remove scheduler : {scheduler.SchedulerName} is not registered.");
                return Task.CompletedTask;
            }
            Volatile.Write(ref removedEntry.Removed, 1);
        }
        Log.LogDebug("Remove scheduler: " + scheduler.SchedulerName);
        return Task.CompletedTask;
    }

    private sealed class SchedulerEntry(ISchedulable scheduler)
    {
        public ISchedulable Scheduler { get; } = scheduler;
        public long LastCallTimestamp;
        public int Removed;
    }
}
