using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPlayPause;

[RegisterSingleton<ICommandHandler>]
public partial class FastPlayPauseCommandHandler : CommandHandlerBase<FastPlayPauseCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
    }

    public override Task Run(Command command)
    {
        EditorDocumentManager.CurrentActivatedEditor?.KeyboardAction_PlayOrPause(default);
        return Task.CompletedTask;
    }
}
