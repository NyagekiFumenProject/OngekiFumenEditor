using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
	[CommandDefinition]
	public class ViewMusicXmlWindowCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.MusicXmlWindow";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.MusicXmlWindow; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}