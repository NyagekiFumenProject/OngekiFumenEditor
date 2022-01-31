using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Commands
{
    [CommandDefinition]
    public class ViewFumenEditorSelectingObjectViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenEditorSelectingObjectViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "当前选择物件查看器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}