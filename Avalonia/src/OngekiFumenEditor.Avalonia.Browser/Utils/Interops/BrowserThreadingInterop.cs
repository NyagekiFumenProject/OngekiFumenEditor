using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace OngekiFumenEditor.Avalonia.Browser.Utils.Interops;

[SupportedOSPlatform("browser")]
internal static partial class BrowserThreadingInterop
{
    /// <summary>
    /// Returns 1 when the response header is enabled, 0 when it is disabled or
    /// absent, and -1 when the browser cannot expose the response header.
    /// </summary>
    [JSImport("globalThis.BrowserThreadingInterop.getCoopHeaderState")]
    public static partial int GetCoopHeaderState();

    /// <summary>
    /// Returns 1 when the response header is enabled, 0 when it is disabled or
    /// absent, and -1 when the browser cannot expose the response header.
    /// </summary>
    [JSImport("globalThis.BrowserThreadingInterop.getCoepHeaderState")]
    public static partial int GetCoepHeaderState();

    [JSImport("globalThis.BrowserThreadingInterop.getSharedArrayBufferState")]
    public static partial int GetSharedArrayBufferState();

    [JSImport("globalThis.BrowserThreadingInterop.getWasmEnableThreadsState")]
    public static partial int GetWasmEnableThreadsState();
}
