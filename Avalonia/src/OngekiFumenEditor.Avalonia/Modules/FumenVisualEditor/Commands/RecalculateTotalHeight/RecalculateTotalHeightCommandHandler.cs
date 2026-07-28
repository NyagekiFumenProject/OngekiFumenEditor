using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight;

[RegisterSingleton<ICommandHandler>]
public partial class RecalculateTotalHeightCommandHandler : CommandHandlerBase<RecalculateTotalHeightCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor?.AudioPlayer is not null;
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
