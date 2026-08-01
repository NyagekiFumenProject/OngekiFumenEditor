namespace OngekiFumenEditor.Avalonia.Avalonia;

public static class Startup
{
    public static void Initialize(string[] args)
    {
        Utils.IPCHelper.Init(args);
    }
}

