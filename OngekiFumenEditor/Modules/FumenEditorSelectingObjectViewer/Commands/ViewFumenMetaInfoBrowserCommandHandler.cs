using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Commands
{
	[CommandHandler]
	public class ViewFumenEditorSelectingObjectViewerCommandHandler : CommandHandlerBase<ViewFumenEditorSelectingObjectViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenEditorSelectingObjectViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenEditorSelectingObjectViewer>();
			return TaskUtility.Completed;
		}
	}
}