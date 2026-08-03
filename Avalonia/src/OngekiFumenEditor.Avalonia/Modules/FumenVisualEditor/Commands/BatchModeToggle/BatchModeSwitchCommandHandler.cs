using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;

[RegisterSingleton<ICommandHandler>]
public partial class BatchModeSwitchCommandHandler : CommandHandlerBase<BatchModeToggleCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
        command.Checked = EditorDocumentManager.CurrentActivatedEditor?.IsBatchMode ?? false;
    }

    public override Task Run(Command command)
    {
        if (EditorDocumentManager.CurrentActivatedEditor is not null)
            EditorDocumentManager.CurrentActivatedEditor.IsBatchMode = !EditorDocumentManager.CurrentActivatedEditor.IsBatchMode;
        return Task.CompletedTask;
    }
}
