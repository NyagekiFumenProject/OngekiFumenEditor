using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands
{
	[CommandHandler]
	public class ViewFumenMetaInfoBrowserCommandHandler : CommandHandlerBase<ViewFumenMetaInfoBrowserCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenMetaInfoBrowserCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenMetaInfoBrowser>();
			return TaskUtility.Completed;
		}
	}
}