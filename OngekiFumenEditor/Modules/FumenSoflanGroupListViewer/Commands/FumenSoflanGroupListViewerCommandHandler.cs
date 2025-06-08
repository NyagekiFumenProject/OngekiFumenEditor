using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Commands
{
	[CommandHandler]
	public class FumenSoflanGroupListViewerCommandHandler : CommandHandlerBase<FumenSoflanGroupListViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public FumenSoflanGroupListViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenSoflanGroupListViewer>();
			return TaskUtility.Completed;
		}
	}
}