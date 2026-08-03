using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch;

[RegisterSingleton<ICommandHandler>]
public partial class EditorModeSwitchCommandHandler : CommandHandlerBase<EditorModeSwitchCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
        command.Checked = EditorDocumentManager.CurrentActivatedEditor?.IsPreviewMode ?? false;
    }

    public override Task Run(Command command)
    {
        EditorDocumentManager.CurrentActivatedEditor?.KeyboardAction_HideOrShow(default);
        return Task.CompletedTask;
    }
}
