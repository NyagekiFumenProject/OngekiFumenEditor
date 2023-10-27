using Caliburn.Micro;
using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Commands
{
	[CommandHandler]
	public class ViewOgkiFumenListBrowserCommandHandler : CommandHandlerBase<ViewOgkiFumenListBrowserCommandDefinition>
	{
		private readonly IWindowManager _windowManager;

		[ImportingConstructor]
		public ViewOgkiFumenListBrowserCommandHandler(IWindowManager windowManager)
		{
			_windowManager = windowManager;
		}

		public override async Task Run(Command command)
		{
			await _windowManager.ShowWindowAsync(IoC.Get<IOgkiFumenListBrowser>());
		}
	}
}