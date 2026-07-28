using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;

[RegisterTransient<IToolboxGenerator>]
public class CommentToolboxGenerator : ToolboxGenerator<Comment>
{
}
