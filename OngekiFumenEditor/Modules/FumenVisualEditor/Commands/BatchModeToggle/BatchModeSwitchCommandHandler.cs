using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Microsoft.Xaml.Behaviors;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BatchModeToggle
{
    [CommandHandler]
    public class BatchModeSwitchCommandHandler : CommandHandlerBase<BatchModeToggleCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public BatchModeSwitchCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
            command.Checked = editorDocumentManager.CurrentActivatedEditor?.IsBatchMode ?? false;
        }

        public override Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor) {
                var behaviors = Interaction.GetBehaviors((FumenVisualEditorView)editor.GetView());

                if (editor.IsBatchMode) {
                    behaviors.Remove(editor.BatchModeBehavior);
                }
                else {
                    behaviors.Add(editor.BatchModeBehavior);
                }
                editor.NotifyOfPropertyChange(nameof(FumenVisualEditorViewModel.IsBatchMode));
                editor.ToastNotify($"{Resources.BatchModeToggle}{(editor.IsBatchMode ? Resources.Enable : Resources.Disable)}");
            }

            return TaskUtility.Completed;
        }
    }
}