using Gekimini.Avalonia.Modules.Toolbox.Models;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base.EditorObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Toolboxes.EditorObjects;

[RegisterSingleton<ToolboxItem>]
public class CommentToolboxGenerator : ToolboxGenerator<Comment>
{
    public CommentToolboxGenerator() : base("Comment", "Misc")
    {
    }
}
