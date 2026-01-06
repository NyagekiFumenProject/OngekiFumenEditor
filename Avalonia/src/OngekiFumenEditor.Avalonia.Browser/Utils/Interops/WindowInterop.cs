using System.Runtime.InteropServices.JavaScript;

namespace OngekiFumenEditor.Avalonia.Browser.Utils.Interops;

public partial class WindowInterop
{
    [JSImport("globalThis.WindowInterop.requestFullScreen")]
    public static partial void RequestFullScreen();

    [JSImport("globalThis.WindowInterop.exitFullScreen")]
    public static partial void ExitFullScreen();

    [JSImport("globalThis.WindowInterop.isFullScreen")]
    public static partial bool IsFullScreen();

    [JSImport("globalThis.WindowInterop.openURL")]
    public static partial bool OpenURL(string url);
    
    [JSImport("globalThis.WindowInterop.getDPI")]
    public static partial double getDPI();
}