using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

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
			get { return Resources.CommandAdjustDockablesHorizonPosition; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}