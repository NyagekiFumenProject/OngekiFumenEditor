using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Kernel.RecentFiles.Commands
{
	[CommandDefinition]
	public class OpenRecentFileCommandListDefinition : CommandListDefinition
	{
		public const string CommandName = "File.OpenRecentFileList";

		public override string Name
		{
			get { return CommandName; }
		}
	}
}
