namespace OngekiFumenEditor.Avalonia.Avalonia;

public abstract class App : Gekimini.Avalonia.App
{
    public bool IsGUIMode { get; }

    protected App(bool isGUIMode = true)
    {
        IsGUIMode = isGUIMode;
    }
}

