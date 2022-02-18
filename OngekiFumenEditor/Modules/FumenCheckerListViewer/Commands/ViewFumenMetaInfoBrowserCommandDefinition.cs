using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands
{
    [CommandDefinition]
    public class ViewFumenCheckerListViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenCheckerListViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "谱面检查器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}