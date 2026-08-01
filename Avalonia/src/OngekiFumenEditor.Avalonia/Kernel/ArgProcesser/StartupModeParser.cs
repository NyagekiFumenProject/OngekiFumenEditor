namespace OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;

public enum StartupMode
{
    Gui,
    Cmd
}

public readonly record struct StartupOptions(StartupMode Mode, string FilePath);

/// <summary>
/// Parses the desktop startup arguments into the application mode. The parser is a pure,
/// allocation-light helper so it stays safe under Native AOT; the Desktop entry point owns
/// executing the two modes.
/// </summary>
public static class StartupModeParser
{
    public const string CmdSwitch = "--cmd";

    public static StartupOptions Parse(string[] args)
    {
        if (args is null || args.Length == 0)
            return new StartupOptions(StartupMode.Gui, null);

        if (args.Contains(CmdSwitch, StringComparer.InvariantCultureIgnoreCase))
            return new StartupOptions(StartupMode.Cmd, null);

        var filePath = args.Length == 1 && File.Exists(args[0]) ? args[0] : null;
        return new StartupOptions(StartupMode.Gui, filePath);
    }
}
