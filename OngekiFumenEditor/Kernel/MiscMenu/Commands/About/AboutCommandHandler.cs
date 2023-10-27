using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.UI.Dialogs;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.About
{
	[CommandHandler]
	public class AboutCommandHandler : CommandHandlerBase<AboutCommandDefinition>
	{
		public override Task Run(Command command)
		{
			new AboutWindow().ShowDialog();
			return TaskUtility.Completed;
		}
	}
}