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
    [ObservableProperty]
    private string mainWindowTitle = "Gekimini.Avalonia for Browser";

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
        get
        {
            Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(Title)}");
            return default;
        }
        set => Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(Title)}");
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
        get
        {
            Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(Icon)}");
            return default;
        }
        set => Log.LogWarn($"BrowserPlatformMainWindow not support get/set {nameof(Icon)}");
    }
}