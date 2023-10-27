using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands
{
	[CommandHandler]
	public class ViewFumenBulletPalleteListViewerCommandHandler : CommandHandlerBase<ViewFumenBulletPalleteListViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenBulletPalleteListViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenBulletPalleteListViewer>();
			return TaskUtility.Completed;
		}
	}
}