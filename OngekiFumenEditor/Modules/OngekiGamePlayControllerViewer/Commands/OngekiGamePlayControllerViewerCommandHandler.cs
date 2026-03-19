using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Commands
{
    [CommandHandler]
    public class OngekiGamePlayControllerViewerCommandHandler : CommandHandlerBase<OngekiGamePlayControllerViewerCommandDefinition>
    {
        public override Task Run(Command command)
        {
            IoC.Get<IShell>().ShowTool<IOngekiGamePlayControllerViewer>();
            return TaskUtility.Completed;
        }
    }
}