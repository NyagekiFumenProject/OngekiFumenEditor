using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.About;

[RegisterSingleton<ICommandHandler>]
public class AboutCommandHandler : CommandHandlerBase<AboutCommandDefinition>
{
    private readonly IWindowManager windowManager;

    public AboutCommandHandler(IWindowManager windowManager)
    {
        this.windowManager = windowManager;
    }

    public override async Task Run(Command command) =>
        await windowManager.ShowDialogAsync(new AboutWindowViewModel());
}
