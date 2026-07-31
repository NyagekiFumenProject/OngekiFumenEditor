using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

[RegisterSingleton<ISplashScreenWindow>]
public class SplashScreenViewModel : WindowViewModelBase, ISplashScreenWindow
{
    public WindowViewModelBase WindowViewModel => this;
}
