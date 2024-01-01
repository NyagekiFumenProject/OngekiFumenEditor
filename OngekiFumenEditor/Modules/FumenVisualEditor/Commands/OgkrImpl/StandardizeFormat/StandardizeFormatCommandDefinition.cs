using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat
{
	[CommandDefinition]
	public class StandardizeFormatCommandDefinition : CommandDefinition
	{
		public const string CommandName = "OngekiFumen.StandardizeFormat";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.StandardizeFormat; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}