using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Toolboxes.EditorObjects
{
    [ToolboxItem(typeof(FumenVisualEditorViewModel), "Comment", "Misc", description: "ToolboxItemComment")]
    public class CommentToolboxGenerator : ToolboxGenerator<Comment>
    {

    }
}
