using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.CallFullGC
{
	[CommandDefinition]
	public class CallFullGCCommandDefinition : CommandDefinition
	{
		public const string CommandName = "File.CallFullGC";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.CommandCallFullGC; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}