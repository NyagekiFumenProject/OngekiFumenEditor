using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer.Commands
{
    [CommandDefinition]
    public class ViewTGridCalculatorToolViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.TGridCalculatorToolViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "时间计算器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}