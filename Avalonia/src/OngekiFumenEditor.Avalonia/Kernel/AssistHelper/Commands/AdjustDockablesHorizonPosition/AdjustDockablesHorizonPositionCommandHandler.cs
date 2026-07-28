using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Impls;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition;

[RegisterSingleton<ICommandHandler>]
public class AdjustDockablesHorizonPositionCommandHandler : CommandHandlerBase<AdjustDockablesHorizonPositionCommandDefinition>
{
    public override void Update(Command command)
    {
        command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
        base.Update(command);
    }

    public override Task Run(Command command)
    {
        var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        if (editor?.Fumen is not null)
            AdjustDockablesHorizonPositionHelper.Execute(editor.Fumen);
        return Task.CompletedTask;
    }
}
