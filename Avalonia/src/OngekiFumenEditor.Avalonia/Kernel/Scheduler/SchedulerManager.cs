using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

[RegisterSingleton<ISchedulerManager>]
internal class SchedulerManager : ISchedulerManager
{
    private readonly List<ISchedulable> schedulers = [];
    private readonly ConcurrentDictionary<ISchedulable, long> schedulersCallTime = [];

    private CancellationTokenSource? runCts;
    private Task? runTask;

    public IEnumerable<ISchedulable> Schedulers => schedulers;

    public Task Init()
    {
        foreach (var scheduler in IoC.GetAll<ISchedulable>())
            _ = AddScheduler(scheduler);

        runCts = new CancellationTokenSource();
        runTask = Task.Run(() => Run(runCts.Token), runCts.Token);
        return Task.CompletedTask;
    }

    public Task AddScheduler(ISchedulable scheduler)
    {
        if (scheduler is null)
        {
            Log.LogWarning("Can't add a null scheduler.");
            return Task.CompletedTask;
        }

        foreach (var existing in schedulers)
        {
            if (existing.SchedulerName.Equals(scheduler.SchedulerName, StringComparison.Ordinal))
            {
                Log.LogWarning($"Can't add scheduler : {scheduler.SchedulerName} is already registered.");
                return Task.CompletedTask;
            }
        }

        schedulers.Add(scheduler);
        schedulersCallTime[scheduler] = 0;
        Log.LogDebug("Added new scheduler: " + scheduler.SchedulerName);
        return Task.CompletedTask;
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        var pending = new List<Task>(16);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                pending.Clear();
                var nowTimestamp = Stopwatch.GetTimestamp();

                foreach (var scheduler in schedulers)
                {
                    if (scheduler is null)
                        continue;

                    var lastTimestamp = schedulersCallTime[scheduler];
                    if (Stopwatch.GetElapsedTime(lastTimestamp, nowTimestamp) < scheduler.ScheduleCallLoopInterval)
                        continue;

                    pending.Add(InvokeAndStamp(scheduler, cancellationToken));
                }

                if (pending.Count > 0)
                    await Task.WhenAll(pending);
                else
                    await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                Log.LogError($"scheduler loop throw exception:{exception}", exception);
            }
        }
    }

    private async Task InvokeAndStamp(ISchedulable scheduler, CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.OnScheduleCall(cancellationToken);
        }
        finally
        {
            schedulersCallTime[scheduler] = Stopwatch.GetTimestamp();
        }
    }

    public async Task Term()
    {
        Log.LogDebug("call SchedulerManager.Term()");

        if (runCts is not null)
        {
            runCts.Cancel();
            if (runTask is not null)
            {
                try
                {
                    await runTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            runCts.Dispose();
            runCts = null;
            runTask = null;
        }

        foreach (var scheduler in Schedulers)
        {
            Log.LogInfo("Call OnSchedulerTerm() :" + scheduler.SchedulerName);
            scheduler.OnSchedulerTerm();
        }
    }

    public Task RemoveScheduler(ISchedulable scheduler)
    {
        if (scheduler is null)
        {
            Log.LogWarning("Can't remove a null scheduler.");
            return Task.CompletedTask;
        }

        var removed = false;
        for (var index = 0; index < schedulers.Count; index++)
        {
            if (!schedulers[index].SchedulerName.Equals(scheduler.SchedulerName, StringComparison.Ordinal))
                continue;

            schedulers.RemoveAt(index);
            schedulersCallTime.TryRemove(scheduler, out _);
            removed = true;
            break;
        }

        if (!removed)
        {
            Log.LogWarning($"Can't remove scheduler : {scheduler.SchedulerName} is not registered.");
            return Task.CompletedTask;
        }

        Log.LogDebug("Remove scheduler: " + scheduler.SchedulerName);
        return Task.CompletedTask;
    }
}

