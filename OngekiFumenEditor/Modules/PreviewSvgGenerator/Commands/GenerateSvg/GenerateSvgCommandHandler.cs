using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg
{
    [CommandHandler]
    public class GenerateSvgCommandHandler : CommandHandlerBase<GenerateSvgCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public GenerateSvgCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
        }

        public override Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
                IoC.Get<IPreviewSvgGenerator>().GenerateSvgAsync(editor, new GenerateOption());
            return TaskUtility.Completed;
        }
    }
}