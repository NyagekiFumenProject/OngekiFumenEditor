using Caliburn.Micro;
using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
	[CommandHandler]
	public class ViewJacketGeneratorWindowCommandHandler : CommandHandlerBase<ViewJacketGeneratorWindowCommandDefinition>
	{
		private readonly IWindowManager _windowManager;

		[ImportingConstructor]
		public ViewJacketGeneratorWindowCommandHandler(IWindowManager windowManager)
		{
			_windowManager = windowManager;
		}

		public override async Task Run(Command command)
		{
			await _windowManager.ShowWindowAsync(IoC.Get<IJacketGenerator>());
		}
	}
}