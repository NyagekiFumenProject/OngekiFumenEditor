using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ClearHistory;

[RegisterSingleton<ICommandHandler>]
public partial class ClearHistoryCommandHandler : CommandHandlerBase<ClearHistoryCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor?.UndoRedoManager is { CanRedo: true } ||
                          EditorDocumentManager.CurrentActivatedEditor?.UndoRedoManager is { CanUndo: true };
    }

    public override Task Run(Command command)
    {
        EditorDocumentManager.CurrentActivatedEditor?.UndoRedoManager?.Clear();
        return Task.CompletedTask;
    }
}
