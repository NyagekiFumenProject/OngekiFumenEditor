using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler :
    CommandHandlerBase<ViewFumenMetaInfoBrowserCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        Shell.ShowTool(IoC.Get<IFumenMetaInfoBrowser>());
        return Task.CompletedTask;
    }
}

