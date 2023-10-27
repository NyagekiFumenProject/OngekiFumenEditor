using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.Commands
{
	[CommandHandler]
	public class FumenEditorRenderControlViewerCommandHandler : CommandHandlerBase<FumenEditorRenderControlViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public FumenEditorRenderControlViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenEditorRenderControlViewer>();
			return TaskUtility.Completed;
		}
	}
}