using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

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
			get { return Resources.OgkiFumenListBrowser; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}