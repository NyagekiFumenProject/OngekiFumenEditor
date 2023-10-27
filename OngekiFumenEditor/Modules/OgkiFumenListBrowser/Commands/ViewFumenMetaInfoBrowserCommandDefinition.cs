using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Commands
{
	[CommandDefinition]
	public class ViewOgkiFumenListBrowserCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.OgkiFumenListBrowser";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "音击谱面库浏览器"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}