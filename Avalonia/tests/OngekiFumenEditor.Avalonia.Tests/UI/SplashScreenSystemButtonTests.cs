using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Modules.Window.Views;
using Iciclecreek.Avalonia.WindowManager;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class SplashScreenSystemButtonTests
{
    // Splash 窗口已按平台拆分到 Desktop/Browser 程序集，这里用一个与 Splash 窗口
    // 同配置(CanResize=false)的最小窗口保住系统按钮显隐的回归行为。
    private sealed class SplashLikeWindow : WindowViewBase
    {
        public SplashLikeWindow()
        {
            CanResize = false;
        }
    }

    [AvaloniaFact]
    public void SplashScreenView_HidesDisabledMinMaxButtons()
    {
        var panel = new WindowsPanel();
        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        try
        {
            var view = new SplashLikeWindow();
            panel.Show(view);

            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
            var minimize = buttons.FirstOrDefault(x => x.Name == "PART_MinimizeButton");
            var maximize = buttons.FirstOrDefault(x => x.Name == "PART_MaximizeButton");

            Assert.NotNull(minimize);
            Assert.NotNull(maximize);

            Assert.False(minimize!.IsVisible, "minimize button should be hidden when CanResize=false");
            Assert.False(maximize!.IsVisible, "maximize button should be hidden when CanResize=false");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResizableWindow_KeepsSystemButtonsVisible()
    {
        var panel = new WindowsPanel();
        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        try
        {
            var view = new WindowViewBase();
            panel.Show(view);

            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
            var minimize = buttons.FirstOrDefault(x => x.Name == "PART_MinimizeButton");
            var maximize = buttons.FirstOrDefault(x => x.Name == "PART_MaximizeButton");

            Assert.NotNull(minimize);
            Assert.NotNull(maximize);

            Assert.True(minimize!.IsVisible, "minimize button should stay visible when CanResize=true");
            Assert.True(maximize!.IsVisible, "maximize button should stay visible when CanResize=true and state=normal");
        }
        finally
        {
            window.Close();
        }
    }
}
