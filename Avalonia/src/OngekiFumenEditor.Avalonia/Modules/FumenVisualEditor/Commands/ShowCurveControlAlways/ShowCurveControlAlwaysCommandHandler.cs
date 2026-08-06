using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;

[RegisterSingleton<ICommandHandler>]
public partial class ShowCurveControlAlwaysCommandHandler : CommandHandlerBase<ShowCurveControlAlwaysCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override Task Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
        command.Checked = EditorDocumentManager.CurrentActivatedEditor?.IsShowCurveControlAlways ?? false;
        return Task.CompletedTask;
    }

    public override Task Run(Command command)
    {
        if (EditorDocumentManager.CurrentActivatedEditor is { } editor)
            editor.IsShowCurveControlAlways = !editor.IsShowCurveControlAlways;
        return Task.CompletedTask;
    }
}
