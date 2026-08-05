using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;

[RegisterSingleton<ICommandHandler>]
public partial class InterpolateAllCommandHandler : CommandHandlerBase<InterpolateAllCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override Task Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
        return Task.CompletedTask;
    }

    public override Task Run(Command command)
    {
        EditorDocumentManager.CurrentActivatedEditor?.ToastNotify("InterpolateAll is not migrated yet.");
        return Task.CompletedTask;
    }
}
