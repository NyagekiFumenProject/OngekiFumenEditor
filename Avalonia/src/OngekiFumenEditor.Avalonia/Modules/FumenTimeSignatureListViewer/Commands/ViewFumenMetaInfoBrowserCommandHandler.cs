using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewFumenTimeSignatureListViewerCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenTimeSignatureListViewer>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}