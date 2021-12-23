using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands
{
    [CommandDefinition]
    public class ViewFumenMetaInfoBrowserCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenMetaInfoBrowser";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "谱面文件信息"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}