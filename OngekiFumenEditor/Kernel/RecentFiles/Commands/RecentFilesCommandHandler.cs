using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.RecentFiles.Commands
{
	[CommandHandler]
	public class RecentFilesCommandHandler : CommandHandlerBase<RecentFilesCommandDefinition>
	{
		private readonly IEditorRecentFilesManager recentOpenedManager;

		[ImportingConstructor]
		public RecentFilesCommandHandler(IEditorRecentFilesManager recentOpenedManager)
		{
			this.recentOpenedManager = recentOpenedManager;
		}

		public override void Update(Command command)
		{
			command.Enabled = recentOpenedManager.RecentRecordInfos.Any();
		}

		public override Task Run(Command command)
		{
			return Task.CompletedTask;
		}
	}
}
