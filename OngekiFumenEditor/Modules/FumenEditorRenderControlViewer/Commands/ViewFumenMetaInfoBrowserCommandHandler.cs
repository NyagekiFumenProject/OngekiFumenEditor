using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.Commands
{
    [CommandHandler]
    public class ModulesFumenEditorRenderControlViewerCommandHandler : CommandHandlerBase<FumenEditorRenderControlViewerCommandDefinition>
    {
        private readonly IShell _shell;

        [ImportingConstructor]
        public ModulesFumenEditorRenderControlViewerCommandHandler(IShell shell)
        {
            _shell = shell;
        }

        public override Task Run(Command command)
        {
            _shell.ShowTool<IFumenEditorRenderControlViewer>();
            return TaskUtility.Completed;
        }
    }
}