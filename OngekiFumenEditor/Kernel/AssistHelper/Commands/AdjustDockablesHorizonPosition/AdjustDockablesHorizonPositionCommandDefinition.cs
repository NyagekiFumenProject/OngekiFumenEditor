using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition
{
    [CommandDefinition]
    public class AdjustDockablesHorizonPositionCommandDefinition : CommandDefinition
    {
        public const string CommandName = "Assist.AdjustDockablesHorizonPosition";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "自动调整所有可靠物件的水平位置"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}