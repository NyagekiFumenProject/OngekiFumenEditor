using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Commands
{
    [CommandDefinition]
    public class ViewFumenObjectPropertyBrowserCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenObjectPropertyBrowser";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "物件属性"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}