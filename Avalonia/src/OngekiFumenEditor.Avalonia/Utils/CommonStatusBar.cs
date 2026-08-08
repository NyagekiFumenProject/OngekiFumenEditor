using Avalonia.Threading;
using Gekimini.Avalonia.Modules.StatusBar;
using Gekimini.Avalonia.Modules.StatusBar.ViewModels;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Utils;

[RegisterSingleton<CommonStatusBar>]
public sealed class CommonStatusBar
{
    public CommonStatusBar(IStatusBar statusBar)
    {
        ArgumentNullException.ThrowIfNull(statusBar);
        if (statusBar.Items.Count < 3)
            throw new InvalidOperationException("The status bar must provide main, left, and right items.");

        MainContentViewModel = statusBar.Items[0];
        SubLeftContentViewModel = statusBar.Items[1];
        SubRightMainContentViewModel = statusBar.Items[2];
    }

    public StatusBarItemViewModel MainContentViewModel { get; }
    public StatusBarItemViewModel SubLeftContentViewModel { get; }
    public StatusBarItemViewModel SubRightMainContentViewModel { get; }

    public void SetMainMessage(string message) => SetMessage(MainContentViewModel, message);
    public void SetSubLeftMessage(string message) => SetMessage(SubLeftContentViewModel, message);
    public void SetSubRightMessage(string message) => SetMessage(SubRightMainContentViewModel, message);

    private static void SetMessage(StatusBarItemViewModel item, string message)
    {
        message ??= string.Empty;
        if (Dispatcher.UIThread.CheckAccess())
        {
            item.Message = message;
            return;
        }

        Dispatcher.UIThread.Post(() => item.Message = message, DispatcherPriority.Background);
    }
}
