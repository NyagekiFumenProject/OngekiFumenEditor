using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class FumenEditorRenderControlViewerCommandHandler : CommandHandlerBase<FumenEditorRenderControlViewerCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenEditorRenderControlViewer>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}