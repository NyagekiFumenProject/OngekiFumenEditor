using System.Runtime.InteropServices.JavaScript;

namespace OngekiFumenEditor.Avalonia.Browser.Utils.Interops;

public partial class JsApplicationInterop
{
    [JSImport("globalThis.JsApplication.exit")]
    public static partial void Exit();
}