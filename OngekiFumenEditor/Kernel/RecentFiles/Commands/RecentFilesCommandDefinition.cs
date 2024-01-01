using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.RecentFiles.Commands
{
	[CommandDefinition]
	public class RecentFilesCommandDefinition : CommandDefinition
	{
		public const string CommandName = "File.RecentFiles";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.CommandRecentFiles; }
		}

		public override string ToolTip => string.Empty;
	}
}
