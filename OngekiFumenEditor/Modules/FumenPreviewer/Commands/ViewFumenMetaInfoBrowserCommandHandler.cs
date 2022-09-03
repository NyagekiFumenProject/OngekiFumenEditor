using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Commands
{
    [CommandHandler]
    public class ViewFumenPreviewerCommandHandler : CommandHandlerBase<ViewFumenPreviewerCommandDefinition>
    {
        private readonly IShell _windowManager;

        [ImportingConstructor]
        public ViewFumenPreviewerCommandHandler(IShell windowManager)
        {
            _windowManager = windowManager;
        }

        public override Task Run(Command command)
        {
            _windowManager.ShowTool(IoC.Get<IFumenPreviewer>());
            return TaskUtility.Completed;
        }
    }
}