using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Commands
{
    [CommandDefinition]
    public class ViewFumenTimeSignatureListViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenTimeSignatureListViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "节拍查看器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}