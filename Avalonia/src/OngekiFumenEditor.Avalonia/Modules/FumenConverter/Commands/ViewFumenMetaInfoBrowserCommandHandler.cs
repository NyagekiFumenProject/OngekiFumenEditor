using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenConverter.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewFumenConverterCommandDefinition>
{
    public override async Task Run(Command command)
    {
        if (IoC.Get<IFumenConverterWindow>() is WindowViewModelBase windowViewModel)
            await IoC.Get<IWindowManager>().ShowWindowAsync(windowViewModel);
    }
}
