using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll
{
	[CommandDefinition]
	public class InterpolateAllCommandDefinition : CommandDefinition
	{
		public const string CommandName = "OngekiFumen.InterpolateAll";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.CommandInterpolateAll; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}

	[CommandDefinition]
	public class InterpolateAllWithXGridLimitCommandDefinition : CommandDefinition
	{
		public const string CommandName = "OngekiFumen.InterpolateAllWithXGridLimit";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.CommandInterpolateAllWithXGridLimit; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}