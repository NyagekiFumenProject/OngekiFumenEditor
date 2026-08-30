using Avalonia;
using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.MainWindow;

[RegisterSingleton<IPlatformMainWindow>]
public partial class BrowserPlatformMainWindow : ObservableObject, IPlatformMainWindow
{
    private const string BrowserIconPath = "./favicon.ico";

    private string mainWindowTitle = "Ongeki Fumen Editor";
    private WindowIcon mainWindowIcon;

    public bool IsFullScreen
    {
        get => Utils.Interops.WindowInterop.IsFullScreen();
        set
        {
            if (value)
                Utils.Interops.WindowInterop.RequestFullScreen();
            else
                Utils.Interops.WindowInterop.ExitFullScreen();
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => mainWindowTitle;
        set
        {
            var nextTitle = value ?? string.Empty;
            var changed = !string.Equals(
                mainWindowTitle,
                nextTitle,
                System.StringComparison.Ordinal);
            mainWindowTitle = nextTitle;
            Utils.Interops.WindowInterop.SetTitle(nextTitle);
            if (changed)
                OnPropertyChanged();
        }
    }

    public Rect? WindowRect
    {
        get
        {
            Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(WindowRect)}");
            return default;
        }
        set => Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(WindowRect)}");
    }

    public WindowIcon Icon
    {
        get => mainWindowIcon;
        set
        {
            mainWindowIcon = value;
            // Avalonia's browser icon loader is a stub because there is no native
            // window to update. Keep the browser tab icon on the host page instead.
            Utils.Interops.WindowInterop.SetIcon(BrowserIconPath);
            OnPropertyChanged();
        }
    }
}
