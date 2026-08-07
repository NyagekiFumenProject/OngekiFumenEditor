using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;

[RegisterSingleton<ICommandHandler>]
public partial class InterpolateAllCommandHandler : InterpolateAllCommandHandlerBase<InterpolateAllCommandDefinition>
{
    public InterpolateAllCommandHandler(
        IEditorDocumentManager editorDocumentManager,
        IDialogManager dialogManager)
        : base(editorDocumentManager, dialogManager, xGridLimit: false)
    {
    }
}

[RegisterSingleton<ICommandHandler>]
public partial class InterpolateAllWithXGridLimitCommandHandler :
    InterpolateAllCommandHandlerBase<InterpolateAllWithXGridLimitCommandDefinition>
{
    public InterpolateAllWithXGridLimitCommandHandler(
        IEditorDocumentManager editorDocumentManager,
        IDialogManager dialogManager)
        : base(editorDocumentManager, dialogManager, xGridLimit: true)
    {
    }
}
