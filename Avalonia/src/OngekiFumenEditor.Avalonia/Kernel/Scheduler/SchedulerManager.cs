using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Concurrent;

namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

[RegisterSingleton<ISchedulerManager>]
internal class SchedulerManager : ISchedulerManager
{
    private readonly List<ISchedulable> schedulers = [];
    private readonly ConcurrentDictionary<ISchedulable, DateTime> schedulersCallTime = [];

    private CancellationTokenSource runCts;
    private Task runTask;

    public IEnumerable<ISchedulable> Schedulers => schedulers;

    public Task Init()
    {
        foreach (var s in IoC.GetAll<ISchedulable>())
            _ = AddScheduler(s);

        runCts = new CancellationTokenSource();
        runTask = Task.Run(() => Run(runCts.Token), runCts.Token);
        return Task.CompletedTask;
    }

    public Task AddScheduler(ISchedulable s)
    {
        if (s is null || schedulers.Any(x => x.SchedulerName.Equals(s.SchedulerName)))
        {
            Log.LogWarning($"Can't add scheduler : {s?.SchedulerName} is null/exist.");
            return Task.CompletedTask;
        }

        schedulers.Add(s);
        schedulersCallTime[s] = DateTime.MinValue;
        Log.LogDebug("Added new scheduler: " + s.SchedulerName);
        return Task.CompletedTask;
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var pending = Schedulers
                    .Where(x => x is not null && DateTime.Now - schedulersCallTime[x] >= x.ScheduleCallLoopInterval)
                    .Select(async x =>
                    {
                        await x.OnScheduleCall(cancellationToken);
                        schedulersCallTime[x] = DateTime.Now;
                    })
                    .ToArray();

                if (pending.Length > 0)
                    await Task.WhenAll(pending);
                else
                    await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Log.LogError($"scheduler loop throw exception:{e}", e);
            }
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
            runCts = default;
            runTask = default;
        }

        foreach (var scheduler in Schedulers)
        {
            Log.LogInfo("Call OnSchedulerTerm() :" + scheduler.SchedulerName);
            scheduler.OnSchedulerTerm();
        }
    }

    public Task RemoveScheduler(ISchedulable s)
    {
        if (s is null || schedulers.All(x => !x.SchedulerName.Equals(s.SchedulerName)))
        {
            Log.LogWarning($"Can't remove scheduler : {s?.SchedulerName} is null or not exist.");
            return Task.CompletedTask;
        }

        schedulers.Remove(s);
        Log.LogDebug("Remove scheduler: " + s.SchedulerName);
        return Task.CompletedTask;
    }
}

