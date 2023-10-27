using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer.Commands
{
	[CommandHandler]
	public class ViewTGridCalculatorToolViewerCommandHandler : CommandHandlerBase<ViewTGridCalculatorToolViewerCommandDefinition>
	{
		private readonly IShell _shell;

		[ImportingConstructor]
		public ViewTGridCalculatorToolViewerCommandHandler(IShell shell)
		{
			_shell = shell;
		}

		public override Task Run(Command command)
		{
			_shell.ShowTool<ITGridCalculatorToolViewer>();
			return TaskUtility.Completed;
		}
	}
}