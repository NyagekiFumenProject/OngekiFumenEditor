using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenObjectPropertyBrowserCommandHandler : CommandHandlerBase<ViewFumenObjectPropertyBrowserCommandDefinition>
{
    private IShell Shell => IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenObjectPropertyBrowser>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}
