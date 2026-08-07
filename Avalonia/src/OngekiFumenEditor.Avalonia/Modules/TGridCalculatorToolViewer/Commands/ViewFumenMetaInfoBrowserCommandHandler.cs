using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer;

namespace OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewTGridCalculatorToolViewerCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<ITGridCalculatorToolViewer>();
        if (tool is Gekimini.Avalonia.Framework.IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}
