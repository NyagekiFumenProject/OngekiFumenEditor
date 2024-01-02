using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.SplashScreen.Commands.ShowSplashScreen;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.SplashScreen.Commands.ShowSplashScreen
{
	[CommandHandler]
	public class ShowSplashScreenCommandHandler : CommandHandlerBase<ShowSplashScreenCommandDefinition>
	{
		private readonly IWindowManager windowManager;

		[ImportingConstructor]
		public ShowSplashScreenCommandHandler(IWindowManager windowManager)
		{
			this.windowManager = windowManager;
		}

		public override Task Run(Command command)
		{
			windowManager.ShowWindowAsync(IoC.Get<ISplashScreenWindow>());
			return TaskUtility.Completed;
		}
	}
}