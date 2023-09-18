using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch
{
    [CommandHandler]
    public class EditorModeSwitchCommandHandler : CommandHandlerBase<EditorModeSwitchCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public EditorModeSwitchCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Checked = editorDocumentManager.CurrentActivatedEditor?.IsPreviewMode ?? false;
        }

        public override Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
                editor.KeyboardAction_HideOrShow();
            command.Checked = editorDocumentManager.CurrentActivatedEditor?.IsPreviewMode ?? false;
            return TaskUtility.Completed;
        }
    }
}