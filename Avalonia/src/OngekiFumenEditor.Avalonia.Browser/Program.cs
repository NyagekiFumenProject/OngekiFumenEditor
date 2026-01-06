using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Media;

namespace OngekiFumenEditor.Avalonia.Browser;

public partial class Program
{
    private static async Task Main(string[] args)
    {
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
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<ExampleBrowserApp>();
    }
}