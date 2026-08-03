using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class ViewFumenVisualEditorSettingsCommandHandler : CommandHandlerBase<ViewFumenVisualEditorSettingsCommandDefinition>
{
    private IShell Shell => IoC.Get<IShell>();

    public override Task Run(Command command)
    {
        var tool = IoC.Get<IFumenVisualEditorSettings>();
        if (tool is IToolViewModel tvm)
            Shell.ShowTool(tvm);
        return Task.CompletedTask;
    }
}