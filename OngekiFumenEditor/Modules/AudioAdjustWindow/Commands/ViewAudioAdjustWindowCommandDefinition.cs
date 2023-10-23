using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow.Commands
{
    [CommandDefinition]
    public class ViewAudioAdjustWindowCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.AudioAdjustWindow";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "音频调整"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}