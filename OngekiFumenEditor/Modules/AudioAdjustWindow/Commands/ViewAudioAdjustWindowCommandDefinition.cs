using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

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
			get { return Resources.CommandAudioAdjustWindow; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}