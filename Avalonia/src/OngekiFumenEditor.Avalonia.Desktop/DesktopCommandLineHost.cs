using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace OngekiFumenEditor.Avalonia.Desktop;

public static class DesktopCommandLineHost
{
    [STAThread]
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return Program.BuildAvaloniaApp(
                () => new OngekiFumenEditorDesktopApp(isGUIMode: false, commandLineArgs: args))
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
