using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Caliburn.Micro;
using Gemini.Modules.Output.ViewModels;
using Gemini.Modules.Output.Views;

namespace OngekiFumenEditor.OutputPaneCheck;

/// <summary>
/// Hosts the real output pane (Gemini.Modules.Output) in a real, off-screen window so that the
/// checks exercise the same view model, dispatcher and TextBox the editor uses. Log lines are
/// always appended from a background thread, the way <c>Log</c>'s drain queue does it.
/// </summary>
internal sealed class OutputPaneHost
{
    private readonly string[] _lines;
    private ScrollViewer? _scrollViewer;
    private int _fed;
    private long _viewUpdates;

    public OutputPaneHost(IReadOnlyList<string> lines, string title)
    {
        if (lines.Count == 0)
            throw new ArgumentException("The log line corpus must not be empty.", nameof(lines));

        _lines = lines.ToArray();

        ViewModel = new OutputViewModel();
        View = new OutputView();
        TextBox = View.FindName("outputText") as TextBox
            ?? throw new InvalidOperationException("OutputView no longer declares the outputText TextBox.");
        TextBox.TextChanged += (_, _) => Interlocked.Increment(ref _viewUpdates);

        Window = new Window
        {
            Title = title,
            Width = 1200,
            Height = 320,
            Left = -4000,
            Top = -4000,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = View,
        };

        ((IViewAware)ViewModel).AttachView(View, null);
    }

    public OutputViewModel ViewModel { get; }

    public OutputView View { get; }

    public TextBox TextBox { get; }

    /// <summary>
    /// The TextBox' internal ScrollViewer. It only exists once the view has been loaded, so it is
    /// resolved on first use - the checks run after the window has been shown.
    /// </summary>
    public ScrollViewer ScrollViewer => _scrollViewer ??= FindScrollViewer(TextBox)
        ?? throw new InvalidOperationException("The output TextBox no longer has a ScrollViewer.");

    public Window Window { get; }

    public int CorpusSize => _lines.Length;

    public int FedLines => _fed;

    public long ViewUpdates => Interlocked.Read(ref _viewUpdates);

    public double VerticalOffset => ScrollViewer.VerticalOffset;

    public double ScrollableHeight => ScrollViewer.ScrollableHeight;

    /// <summary>The pane shows the tail of the log.</summary>
    public bool PinnedToEnd =>
        ScrollableHeight > 0 && Math.Abs(VerticalOffset - ScrollableHeight) <= 1.0;

    /// <summary>Text currently displayed, with normalized line endings.</summary>
    public string ViewText => TextBox.Text.Replace("\r\n", "\n");

    public string ExpectedLines(int start, int count) =>
        string.Concat(Enumerable.Range(0, count).Select(i => _lines[(start + i) % _lines.Length] + "\n"));

    /// <summary>
    /// Appends <paramref name="count"/> lines from a background thread and completes once the UI
    /// thread has applied all of them; returns how long the appending thread was blocked.
    /// </summary>
    public Task<long> FeedAsync(int count)
    {
        var completion = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = _fed;

        new Thread(() =>
        {
            var elapsed = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
                ViewModel.Append(_lines[(start + i) % _lines.Length] + "\n");
            elapsed.Stop();
            Interlocked.Add(ref _fed, count);

            // Flushes are queued at Normal priority, so a Background marker only runs once the view
            // holds every appended line.
            Window.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => completion.SetResult(elapsed.ElapsedMilliseconds));
        })
        { IsBackground = true }.Start();

        return completion.Task;
    }

    /// <summary>Scrolls like a user would and makes the new offset readable.</summary>
    public void ScrollTo(double fraction)
    {
        ScrollViewer.UpdateLayout();
        ScrollViewer.ScrollToVerticalOffset(ScrollableHeight * fraction);
        ScrollViewer.UpdateLayout();
    }

    public void Settle() => ScrollViewer.UpdateLayout();

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is Control control && control.Template?.FindName("PART_ContentHost", control) is ScrollViewer host)
            return host;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, i)) is { } result)
                return result;
        }

        return null;
    }
}
