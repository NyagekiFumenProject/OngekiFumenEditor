using Caliburn.Micro;
using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
	[CommandHandler]
	public class ViewAcbGeneratorWindowCommandHandler : CommandHandlerBase<ViewAcbGeneratorWindowCommandDefinition>
	{
		private readonly IWindowManager _windowManager;

		[ImportingConstructor]
		public ViewAcbGeneratorWindowCommandHandler(IWindowManager windowManager)
		{
			_windowManager = windowManager;
		}

		public override async Task Run(Command command)
		{
			await _windowManager.ShowWindowAsync(IoC.Get<IAcbGenerator>());
		}
	}
}