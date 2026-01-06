using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.MainWindow;

[RegisterSingleton<IPlatformMainWindow>]
public partial class BrowserPlatformMainWindow : ObservableObject, IPlatformMainWindow
{
    [ObservableProperty]
    private string mainWindowTitle = "Gekimini.Avalonia for Browser";

    [GetServiceLazy]
    private partial ILogger<BrowserPlatformMainWindow> Logger { get; }

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
            Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(Title)}");
            return default;
        }
        set => Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(Title)}");
    }

    public Rect? WindowRect
    {
        get
        {
            Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(WindowRect)}");
            return default;
        }
        set => Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(WindowRect)}");
    }

    public WindowIcon Icon
    {
        get
        {
            Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(Icon)}");
            return default;
        }
        set => Logger.LogWarningEx($"BrowserPlatformMainWindow not support get/set {nameof(Icon)}");
    }
}