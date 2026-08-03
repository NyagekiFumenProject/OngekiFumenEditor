using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewFumenEditorSelectingObjectViewerCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenMetaInfoBrowser>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}
