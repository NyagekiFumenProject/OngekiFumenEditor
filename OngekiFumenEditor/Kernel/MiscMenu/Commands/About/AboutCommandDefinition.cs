using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.About
{
	[CommandDefinition]
	public class AboutCommandDefinition : CommandDefinition
	{
		public const string CommandName = "Help.About";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "关于"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}