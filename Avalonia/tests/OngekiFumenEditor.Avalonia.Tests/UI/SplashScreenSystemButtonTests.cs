using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Modules.Window.Views;
using Iciclecreek.Avalonia.WindowManager;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen.Views;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class SplashScreenSystemButtonTests
{
    [AvaloniaFact]
    public void SplashScreenView_HidesDisabledMinMaxButtons()
    {
        var panel = new WindowsPanel();
        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        try
        {
            var view = new SplashScreenView();
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
