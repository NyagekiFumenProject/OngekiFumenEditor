using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.ProgramUpdater.Commands.About
{
	[CommandDefinition]
	public class ShowNewVersionCommandDefinition : CommandDefinition
	{
		public const string CommandName = "ProgramUpdater.StartUpdate";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "开始更新"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}