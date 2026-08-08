using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Gekimini.Avalonia.Modules.StatusBar;
using Gekimini.Avalonia.Modules.StatusBar.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using StatusBar.Avalonia;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class CommonStatusBarTests
{
    [AvaloniaFact]
    public async Task Bridge_UsesRealStatusBarItemsAndDispatchesBackgroundUpdates()
    {
        var statusBar = IoC.Get<IStatusBar>();
        var bridge = IoC.Get<CommonStatusBar>();
        var original = statusBar.Items.Select(item => item.Message).ToArray();

        Assert.Same(statusBar.Items[0], bridge.MainContentViewModel);
        Assert.Same(statusBar.Items[1], bridge.SubLeftContentViewModel);
        Assert.Same(statusBar.Items[2], bridge.SubRightMainContentViewModel);

        try
        {
            bridge.SetMainMessage("main");
            bridge.SetSubLeftMessage("left");
            bridge.SetSubRightMessage("right");

            Assert.Equal("main", statusBar.Items[0].Message);
            Assert.Equal("left", statusBar.Items[1].Message);
            Assert.Equal("right", statusBar.Items[2].Message);

            await Task.Run(() => bridge.SetMainMessage("background"));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

            Assert.Equal("background", statusBar.Items[0].Message);
        }
        finally
        {
            for (var index = 0; index < original.Length && index < statusBar.Items.Count; index++)
                statusBar.Items[index].Message = original[index];
        }
    }

    [Fact]
    public void Constructor_IncompleteStatusBar_FailsFast()
    {
        var items = new[]
        {
            new StatusBarItemViewModel(string.Empty, GridLength.Auto),
            new StatusBarItemViewModel(string.Empty, GridLength.Auto)
        };

        Assert.Throws<InvalidOperationException>(() => new CommonStatusBar(new IncompleteStatusBar(items)));
    }

    private sealed class IncompleteStatusBar(IReadOnlyList<StatusBarItemViewModel> items) : IStatusBar
    {
        public StatusBarManager StatusBarManager => null!;
        public IReadOnlyList<StatusBarItemViewModel> Items { get; } = items;
    }
}
