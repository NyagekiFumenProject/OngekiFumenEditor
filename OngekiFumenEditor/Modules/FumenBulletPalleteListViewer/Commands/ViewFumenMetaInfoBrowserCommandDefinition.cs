using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands
{
    [CommandDefinition]
    public class ViewFumenBulletPalleteListViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenBulletPalleteListViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "子弹管理"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}