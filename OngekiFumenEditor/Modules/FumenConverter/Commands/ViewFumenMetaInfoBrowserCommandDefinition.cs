using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

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
			get { return Resources.FumenConverter; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}