using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler :
    CommandHandlerBase<ViewFumenMetaInfoBrowserCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();
    private IServiceProvider ServiceProvider => OngekiFumenEditor.Avalonia.IoC.Get<IServiceProvider>();

    public override Task Run(Command command)
    {
        Shell.ShowTool(ServiceProvider.Resolve<FumenMetaInfoBrowserViewModel>());
        return Task.CompletedTask;
    }
}

