using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Kernel.EditorLayout.Commands.ApplySuggestEditorLayout;

[RegisterSingleton<ICommandHandler>]
public class ApplySuggestEditorLayoutCommandHandler : CommandHandlerBase<ApplySuggestEditorLayoutCommandDefinition>
{
    public override async Task Run(Command command)
    {
        await IoC.Get<IEditorLayoutManager>().ApplyDefaultSuggestEditorLayout();
    }
}
