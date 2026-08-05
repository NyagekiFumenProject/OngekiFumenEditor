using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight;

[RegisterSingleton<ICommandHandler>]
public partial class RecalculateTotalHeightCommandHandler : CommandHandlerBase<RecalculateTotalHeightCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override Task Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor?.AudioPlayer is not null;
        return Task.CompletedTask;
    }

    public override Task Run(Command command)
    {
        if (EditorDocumentManager.CurrentActivatedEditor is { AudioPlayer: { } audioPlayer, EditorProjectData: { } editorProjectData } editor)
        {
            editorProjectData.AudioDuration = audioPlayer.Duration;
            editor.RecalculateTotalDurationHeight();
        }

        return Task.CompletedTask;
    }
}
