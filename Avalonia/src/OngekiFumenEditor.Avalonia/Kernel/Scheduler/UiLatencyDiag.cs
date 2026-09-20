using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Gekimini.Avalonia.Framework.Commands;

namespace OngekiFumenEditor.Avalonia.Kernel.Scheduler;

/// <summary>
/// Temporary latency diagnostics: count suspicious frequent events and dump
/// the counters periodically. Remove once the browser input latency is solved.
/// </summary>
internal static class UiLatencyDiag
{
    public static long RequeryCount;
    public static long MusicPropsCount;
    private static readonly ConcurrentDictionary<string, long> SchedulerQueued = new();
    private static Task dumpLoop;

    public static void CountSchedulerQueued(string schedulerName) =>
        SchedulerQueued.AddOrUpdate(schedulerName, 1, static (_, v) => v + 1);

    public static void Start()
    {
        if (dumpLoop is { IsCompleted: false })
            return;

        CommandManager.RequerySuggested += static (_, _) => Interlocked.Increment(ref RequeryCount);
        dumpLoop = Task.Run(DumpLoop);
    }

    private static async Task DumpLoop()
    {
        while (true)
        {
            await Task.Delay(5000).ConfigureAwait(false);
            var perScheduler = string.Join(",", SchedulerQueued.Select(static kv => $"{kv.Key}={kv.Value}"));
            Utils.Log.LogInfo($"[UiLatencyDiag] requery={Interlocked.Read(ref RequeryCount)} " +
                              $"musicProps={Interlocked.Read(ref MusicPropsCount)} queued {{{perScheduler}}}");
        }
    }
}
