namespace OngekiFumenEditor.Avalonia.Avalonia;

public class AppBootstrapper
{
    public bool IsGUIMode { get; private set; } = true;

    public void OnStartupForGUI() => IsGUIMode = true;

    public void OnStartupForCMD() => IsGUIMode = false;
}

