using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Kernel.Scheduler;

public sealed class SchedulerManagerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [AvaloniaFact]
    public async Task SlowAndThrowingCallbacks_DoNotBlockOtherSchedulersOrOverlap()
    {
        var manager = new SchedulerManager();
        var slowStarted = NewSignal();
        var releaseSlow = NewSignal();
        var fastRepeated = NewSignal();
        var failureRepeated = NewSignal();
        var slowCalls = 0;
        var fastCalls = 0;
        var failures = 0;
        await Start(manager);
        await manager.AddScheduler(new Callback("slow", async _ =>
        {
            Interlocked.Increment(ref slowCalls);
            slowStarted.TrySetResult();
            await releaseSlow.Task;
        }));
        await manager.AddScheduler(new Callback("fast", async _ =>
        {
            await slowStarted.Task;
            if (Interlocked.Increment(ref fastCalls) == 3)
                fastRepeated.TrySetResult();
        }));
        await manager.AddScheduler(new Callback("fault", _ =>
        {
            if (Interlocked.Increment(ref failures) == 3)
                failureRepeated.TrySetResult();
            throw new InvalidOperationException("Expected callback failure");
        }));
        try
        {
            await Task.WhenAll(fastRepeated.Task, failureRepeated.Task).WaitAsync(Timeout);
            Assert.Equal(1, Volatile.Read(ref slowCalls));
        }
        finally
        {
            var termination = manager.Term();
            releaseSlow.TrySetResult();
            await termination.WaitAsync(Timeout);
        }
    }

    [AvaloniaFact]
    public async Task ConcurrentTerm_DrainsRemovedCallbacksThenTerminatesEachSchedulerOnce()
    {
        var manager = new SchedulerManager();
        var started = NewSignal();
        var canceled = NewSignal();
        var release = NewSignal();
        var hooksStarted = NewSignal();
        using var releaseHooks = new ManualResetEventSlim();
        var active = 0;
        var hooks = 0;
        var prematureHooks = 0;
        var firstHookCalls = 0;
        var secondHookCalls = 0;
        void Terminate(ref int calls)
        {
            Interlocked.Increment(ref calls);
            if (Volatile.Read(ref active) != 0)
                Interlocked.Increment(ref prematureHooks);
            if (Interlocked.Increment(ref hooks) == 2)
                hooksStarted.TrySetResult();
            if (!releaseHooks.Wait(Timeout))
                throw new TimeoutException("Termination hooks were not run concurrently");
        }
        var removed = new Callback("removed", async token =>
        {
            Interlocked.Increment(ref active);
            using var registration = token.Register(() => canceled.TrySetResult());
            started.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref active);
        });
        await Start(manager);
        await manager.AddScheduler(removed);
        await manager.AddScheduler(new Callback("first", _ => Task.CompletedTask, () => Terminate(ref firstHookCalls)));
        await manager.AddScheduler(new Callback("second", _ => Task.CompletedTask, () => Terminate(ref secondHookCalls)));
        try
        {
            await started.Task.WaitAsync(Timeout);
            await manager.RemoveScheduler(removed);
            var first = manager.Term();
            var second = manager.Term();
            await canceled.Task.WaitAsync(Timeout);
            Assert.False(first.IsCompleted);
            Assert.False(second.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref hooks));
            release.TrySetResult();
            await hooksStarted.Task.WaitAsync(Timeout);
            releaseHooks.Set();
            await Task.WhenAll(first, second).WaitAsync(Timeout);
            Assert.Equal(0, prematureHooks);
            Assert.Equal(1, firstHookCalls);
            Assert.Equal(1, secondHookCalls);
            Assert.Empty(manager.Schedulers);
        }
        finally
        {
            release.TrySetResult();
            releaseHooks.Set();
            await manager.Term().WaitAsync(Timeout);
        }
    }

    [AvaloniaFact]
    public async Task ReregisteredInstance_IsNotOverlappedOrDelayedByRemovedEntryCompletion()
    {
        var manager = new SchedulerManager();
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        var releaseFirst = NewSignal();
        var calls = 0;
        var active = 0;
        var overlaps = 0;
        var scheduler = new Callback("replace", async _ =>
        {
            if (Interlocked.Increment(ref active) != 1)
                Interlocked.Increment(ref overlaps);
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }
            else
                secondStarted.TrySetResult();
            Interlocked.Decrement(ref active);
        }) { ScheduleCallLoopInterval = TimeSpan.FromDays(1) };
        await Start(manager);
        await manager.AddScheduler(scheduler);
        try
        {
            await firstStarted.Task.WaitAsync(Timeout);
            await manager.RemoveScheduler(scheduler);
            await manager.AddScheduler(scheduler);
            releaseFirst.TrySetResult();
            await secondStarted.Task.WaitAsync(Timeout);
            Assert.Equal(0, overlaps);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await manager.Term().WaitAsync(Timeout);
        }
    }

    [AvaloniaFact]
    public async Task ConcurrentRegistration_DeduplicatesByOrdinalName()
    {
        var manager = new SchedulerManager();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            manager.AddScheduler(new Callback("same", _ => Task.CompletedTask)))));
        await manager.AddScheduler(new Callback("SAME", _ => Task.CompletedTask));
        Assert.Equal(new[] { "SAME", "same" }, manager.Schedulers.Select(x => x.SchedulerName).Order(StringComparer.Ordinal));
        await manager.RemoveScheduler(new Callback("same", _ => Task.CompletedTask));
        Assert.Equal("SAME", Assert.Single(manager.Schedulers).SchedulerName);
        await manager.Term().WaitAsync(Timeout);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task Start(SchedulerManager manager)
    {
        await manager.Init();
        // Do not terminate the application's shared DI schedulers with this local manager.
        foreach (var scheduler in manager.Schedulers)
            await manager.RemoveScheduler(scheduler);
    }

    private sealed class Callback(string name, Func<CancellationToken, Task> call, Action? terminate = null) : ISchedulable
    {
        public string SchedulerName => name;
        public TimeSpan ScheduleCallLoopInterval { get; init; } = TimeSpan.FromMilliseconds(10);
        public Task OnScheduleCall(CancellationToken cancellationToken) => call(cancellationToken);
        public void OnSchedulerTerm() => terminate?.Invoke();
    }
}
