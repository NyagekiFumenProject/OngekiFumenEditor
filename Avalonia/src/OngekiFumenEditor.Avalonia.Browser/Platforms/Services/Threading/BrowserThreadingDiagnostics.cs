using System.Runtime.Versioning;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Threading;

[SupportedOSPlatform("browser")]
[RegisterSingleton<IThreadingDiagnostics>]
public sealed class BrowserThreadingDiagnostics : IThreadingDiagnostics
{
    public ThreadingDiagnosticsSnapshot GetSnapshot()
    {
        var threadIds = ThreadingDiagnosticsRuntime.GetThreadIds();
        return new ThreadingDiagnosticsSnapshot(
            CoopHeaderEnabled: ToNullable(BrowserThreadingInterop.GetCoopHeaderState()),
            CoepHeaderEnabled: ToNullable(BrowserThreadingInterop.GetCoepHeaderState()),
            SharedArrayBufferSupported: ToNullable(BrowserThreadingInterop.GetSharedArrayBufferState()),
            WasmEnableThreadsEnabled: ToNullable(BrowserThreadingInterop.GetWasmEnableThreadsState()),
            MainThreadId: threadIds.MainThreadId,
            UIThreadId: threadIds.UIThreadId,
            RenderThreadId: threadIds.RenderThreadId);
    }

    private static bool? ToNullable(int state) => state switch
    {
        1 => true,
        0 => false,
        _ => null
    };
}
