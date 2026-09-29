using System.Diagnostics;
using System.Windows.Threading;

namespace OngekiFumenEditor.OutputPaneCheck;

/// <summary>
/// Measures how long the UI thread takes to answer an Input-priority operation while the log is
/// being written, i.e. how the editor feels to the user during a log burst.
/// </summary>
internal sealed class InputLatencyProbe : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly List<double> _latencies = new();
    private readonly Thread _thread;
    private volatile bool _stopped;

    public InputLatencyProbe(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        _thread = new Thread(() =>
        {
            while (!_stopped)
            {
                var posted = Stopwatch.GetTimestamp();
                _dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
                {
                    var latency = Stopwatch.GetElapsedTime(posted).TotalMilliseconds;
                    lock (_latencies)
                        _latencies.Add(latency);
                });

                Thread.Sleep(10);
            }
        })
        { IsBackground = true };

        _thread.Start();
    }

    public (double P50, double P95, double Max, int Count) Snapshot()
    {
        double[] sorted;
        lock (_latencies)
            sorted = _latencies.ToArray();

        if (sorted.Length == 0)
            return (0, 0, 0, 0);

        Array.Sort(sorted);
        return (Percentile(sorted, 0.50), Percentile(sorted, 0.95), Percentile(sorted, 1.00), sorted.Length);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        var index = (int)Math.Min(sorted.Length - 1, Math.Ceiling(percentile * sorted.Length) - 1);
        return sorted[Math.Max(0, index)];
    }

    public void Dispose()
    {
        _stopped = true;
        _thread.Join(1000);
    }
}
