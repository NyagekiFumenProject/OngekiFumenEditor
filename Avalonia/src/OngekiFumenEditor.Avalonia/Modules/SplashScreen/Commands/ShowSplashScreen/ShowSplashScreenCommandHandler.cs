using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands.ShowSplashScreen;

[RegisterSingleton<ICommandHandler>]
public partial class ShowSplashScreenCommandHandler : CommandHandlerBase<ShowSplashScreenCommandDefinition>
{
    private IWindowManager WindowManager => OngekiFumenEditor.Avalonia.IoC.Get<IWindowManager>();
    private ISplashScreenWindow SplashScreenWindow => OngekiFumenEditor.Avalonia.IoC.Get<ISplashScreenWindow>();

    public override async Task Run(Command command)
    {
        await WindowManager.ShowWindowAsync(SplashScreenWindow.WindowViewModel);
    }
}
