using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation;

namespace OngekiFumenEditor.Avalonia.Browser;

public partial class Program
{
    private static async Task Main(string[] args)
    {
        ThreadingDiagnosticsRuntime.CaptureMainThread();
        await BuildAvaloniaApp()
            .WithInterFont()
            .With(new FontManagerOptions
            {
                FontFallbacks = new[]
                {
                    new FontFallback
                    {
                        FontFamily =
                            new FontFamily(
                                "avares://OngekiFumenEditor.Avalonia.Browser/Assets/Fonts/NotoSansSC-Regular.ttf#Noto Sans SC")
                    }
                }
            })
            .StartBrowserAppAsync("out", new BrowserPlatformOptions
            {
                // Avalonia 12.1.1 does not wake its managed dispatcher when input is queued.
                // Keep WASM threads and the render worker, but use browser event-loop dispatch.
                PreferManagedThreadDispatcher = false
            });
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<OngekiFumenEditorBrowserApp>();
    }
}