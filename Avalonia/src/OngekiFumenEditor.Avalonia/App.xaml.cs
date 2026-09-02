using Avalonia;
using Avalonia.Markup.Xaml;

namespace OngekiFumenEditor.Avalonia;

public abstract class App : Gekimini.Avalonia.App
{
    public bool IsGUIMode { get; }

    protected override bool ShouldCreateMainView => IsGUIMode;

    protected App(bool isGUIMode = true)
    {
        IsGUIMode = isGUIMode;
    }

    public override void Initialize()
    {
        base.Initialize();

        AvaloniaXamlLoader.Load(this);
    }
}
