using Caliburn.Micro;
using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenConverter.Commands
{
	[CommandHandler]
	public class ViewFumenConverterCommandHandler : CommandHandlerBase<ViewFumenConverterCommandDefinition>
	{
		private readonly IWindowManager _windowManager;

		[ImportingConstructor]
		public ViewFumenConverterCommandHandler(IWindowManager windowManager)
		{
			_windowManager = windowManager;
		}

		public override async Task Run(Command command)
		{
			await _windowManager.ShowWindowAsync(IoC.Get<IFumenConverter>());
		}
	}
}