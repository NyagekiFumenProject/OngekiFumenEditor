using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.SvgToLaneBrowser.Commands
{
    [CommandDefinition]
    public class ViewSvgToLaneBrowserCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.SvgToLaneBrowser";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "Svg转轨道生成器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}