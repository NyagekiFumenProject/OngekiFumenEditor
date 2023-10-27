using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
	[CommandDefinition]
	public class ViewAcbGeneratorWindowCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.AcbGenerator";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "音频文件生成器"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}