using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

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
			get { return Resources.AcbGenerator; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}