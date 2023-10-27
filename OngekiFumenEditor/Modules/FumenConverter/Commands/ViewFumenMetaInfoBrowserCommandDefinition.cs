using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenConverter.Commands
{
	[CommandDefinition]
	public class ViewFumenConverterCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenConverter";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "谱面文件转换"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}