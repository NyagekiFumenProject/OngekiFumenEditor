using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.ProgramUpdater;
using OngekiFumenEditor.Kernel.ProgramUpdater.Dialogs.ViewModels;
using OngekiFumenEditor.UI.Dialogs;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater.Commands.About
{
    [CommandHandler]
    public class ShowNewVersionCommandHandler : CommandHandlerBase<ShowNewVersionCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await IoC.Get<IWindowManager>().ShowWindowAsync(new ShowNewVersionDialogViewModel());
        }
    }
}