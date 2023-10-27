using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Commands
{
	[CommandHandler]
	public class ViewFumenObjectPropertyBrowserCommandHandler : CommandHandlerBase<ViewFumenObjectPropertyBrowserCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewFumenObjectPropertyBrowserCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<IFumenObjectPropertyBrowser>();
			return TaskUtility.Completed;
		}
	}
}