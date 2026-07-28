namespace OngekiFumenEditor.Avalonia.Avalonia;

internal static class Startup
{
    public static void Initialize(string[] args)
    {
        Utils.IPCHelper.Init(args);
    }
}

