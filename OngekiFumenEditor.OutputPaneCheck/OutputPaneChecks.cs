using System.Diagnostics;
using System.Windows.Threading;

namespace OngekiFumenEditor.OutputPaneCheck;

/// <summary>
/// Drives the output pane through the behaviour the editor relies on: following the tail while
/// "自动滑动" is on, keeping the reader's position while it is off, and staying consistent across
/// clear/append sequences.
/// </summary>
internal sealed class OutputPaneChecks
{
    private readonly OutputPaneHost _host;
    private readonly int _perfLines;
    private readonly bool _runPerf;
    private int _failed;

    public OutputPaneChecks(OutputPaneHost host, int perfLines, bool runPerf)
    {
        _host = host;
        _perfLines = perfLines;
        _runPerf = runPerf;
    }

    public int Failures => _failed;

    public async Task RunAsync()
    {
        Console.WriteLine($"pane   : {_host.CorpusSize} line corpus, viewport {_host.ScrollViewer.ViewportHeight:F0} px, " +
                          $"font {_host.TextBox.FontFamily}, scrollbar " +
                          $"{System.Windows.Controls.ScrollViewer.GetVerticalScrollBarVisibility(_host.TextBox)}");

        await RunAutoScrollChecksAsync();
        await RunClearChecksAsync();

        if (_runPerf)
            await RunPerformanceCheckAsync();

        Console.WriteLine(_failed == 0 ? "\nall checks passed" : $"\n{_failed} check(s) FAILED");
    }

    private async Task RunAutoScrollChecksAsync()
    {
        Console.WriteLine("\nauto scroll (自动滑动):");

        // A fresh pane has auto scroll on and no content yet.
        await _host.FeedAsync(_host.CorpusSize);
        _host.Settle();
        Check("auto scroll on: follows the tail", _host.PinnedToEnd);

        // Toggled off + the reader scrolled to the top: new lines must not move the view.
        _host.ViewModel.ToggleAutoScrollEnd();
        _host.ScrollTo(0.0);
        var topOffset = _host.VerticalOffset;
        var textAtTop = _host.TextBox.Text.Length;

        await _host.FeedAsync(200);
        _host.Settle();
        Check("auto scroll off: reader's position kept (top)", Math.Abs(_host.VerticalOffset - topOffset) <= 1.0);
        Check("auto scroll off: content still grows", _host.TextBox.Text.Length > textAtTop);

        // Middle of the document.
        _host.ScrollTo(0.5);
        var middleOffset = _host.VerticalOffset;

        await _host.FeedAsync(100);
        _host.Settle();
        Check("auto scroll off: reader's position kept (middle)", Math.Abs(_host.VerticalOffset - middleOffset) <= 1.0);

        // Reader sitting at the bottom: the pane must stay where it is instead of chasing the tail.
        _host.ScrollTo(1.0);
        var bottomOffset = _host.VerticalOffset;

        await _host.FeedAsync(50);
        _host.Settle();
        Check("auto scroll off: does not follow new lines", !_host.PinnedToEnd && _host.ScrollableHeight > bottomOffset);
        Check("auto scroll off: reader's position kept (bottom)", Math.Abs(_host.VerticalOffset - bottomOffset) <= 1.0);

        // Toggled back on: the next update pins the pane to the tail again.
        _host.ViewModel.ToggleAutoScrollEnd();
        await _host.FeedAsync(5);
        _host.Settle();
        Check("auto scroll on again: pinned back to the tail", _host.PinnedToEnd);
    }

    private async Task RunClearChecksAsync()
    {
        Console.WriteLine("\nclear (清理):");

        _host.ViewModel.Clear();
        _host.Settle();
        Check("clear: view emptied", _host.TextBox.Text.Length == 0);

        var firstLineAfterClear = _host.FedLines;
        await _host.FeedAsync(20);
        _host.Settle();
        Check("clear: text restarts without loss or duplicates",
            _host.ViewText == _host.ExpectedLines(firstLineAfterClear, 20));
        Check("clear: still follows the tail", _host.PinnedToEnd);
    }

    private async Task RunPerformanceCheckAsync()
    {
        Console.WriteLine("\nthroughput (informational):");

        _host.ViewModel.Clear();
        _host.Settle();

        using var probe = new InputLatencyProbe(Dispatcher.CurrentDispatcher);
        var stopwatch = Stopwatch.StartNew();
        var callerBlockedMs = await _host.FeedAsync(_perfLines);
        stopwatch.Stop();

        var (p50, p95, max, samples) = probe.Snapshot();
        var text = _host.TextBox.Text.Length;
        var rate = _perfLines / Math.Max(0.0005, stopwatch.Elapsed.TotalSeconds);

        Console.WriteLine($"  {_perfLines} lines / {text / 1024.0 / 1024.0:F2} MiB of log text");
        Console.WriteLine($"  appending thread blocked {callerBlockedMs,6} ms, " +
                          $"UI settled after {stopwatch.ElapsedMilliseconds,6} ms ({rate:F0} lines/s)");
        Console.WriteLine($"  view updates (TextChanged) {_host.ViewUpdates}");
        Console.WriteLine($"  input latency p50 {p50:F1} ms, p95 {p95:F1} ms, max {max:F1} ms ({samples} samples)");
    }

    private void Check(string what, bool ok)
    {
        if (!ok)
            _failed++;

        Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}");
    }
}
