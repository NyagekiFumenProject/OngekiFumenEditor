using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Commands
{
    [CommandDefinition]
    public class ViewFumenPreviewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenPreviewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "谱面预览"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}