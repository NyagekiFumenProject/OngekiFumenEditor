using Gemini.Framework.Commands;

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
			get { return "Music.xml生成器"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}