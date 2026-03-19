using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Commands
{
    [CommandDefinition]
    public class OngekiGamePlayControllerViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.OngekiGamePlayControllerViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "Akariの控制器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}