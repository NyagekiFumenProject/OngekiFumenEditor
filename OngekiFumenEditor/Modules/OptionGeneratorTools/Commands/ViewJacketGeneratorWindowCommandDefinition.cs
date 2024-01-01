using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
	[CommandDefinition]
	public class ViewJacketGeneratorWindowCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.JacketGenerator";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.JacketGenerator; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}