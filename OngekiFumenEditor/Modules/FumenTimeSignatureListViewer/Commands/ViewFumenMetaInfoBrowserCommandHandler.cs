using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Commands
{
	[CommandHandler]
	public class ViewFumenTimeSignatureListViewerCommandHandler : CommandHandlerBase<ViewFumenTimeSignatureListViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenTimeSignatureListViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenTimeSignatureListViewer>();
			return TaskUtility.Completed;
		}
	}
}