using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands
{
	[CommandHandler]
	public class ViewFumenCheckerListViewerCommandHandler : CommandHandlerBase<ViewFumenCheckerListViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenCheckerListViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenCheckerListViewer>();
			return TaskUtility.Completed;
		}
	}
}