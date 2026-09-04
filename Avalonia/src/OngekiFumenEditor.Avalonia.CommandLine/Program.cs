using OngekiFumenEditor.Avalonia.Desktop;

namespace OngekiFumenEditor.Avalonia.CommandLine;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);
        return DesktopCommandLineHost.Run(args);
    }
}
