using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;

[RegisterSingleton<ICommandHandler>]
public partial class ShowCurveControlAlwaysCommandHandler : CommandHandlerBase<ShowCurveControlAlwaysCommandDefinition>
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override void Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
    }

    public override Task Run(Command command)
    {
        EditorDocumentManager.CurrentActivatedEditor?.ToastNotify("ShowCurveControlAlways is not migrated yet.");
        return Task.CompletedTask;
    }
}
