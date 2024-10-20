using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Microsoft.Xaml.Behaviors;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BrushModeSwitch
{
    [CommandHandler]
    public class BrushModeSwitchCommandHandler : CommandHandlerBase<BrushModeSwitchCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public BrushModeSwitchCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
            command.Checked = editorDocumentManager.CurrentActivatedEditor?.BrushMode ?? false;
        }

        public override Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor) {
                var behaviors = Interaction.GetBehaviors((FumenVisualEditorView)editor.GetView());

                if (editor.BrushMode) {
                    behaviors.Remove(editor.BrushModeBehavior);
                }
                else {
                    behaviors.Add(editor.BrushModeBehavior);
                }
                editor.NotifyOfPropertyChange(nameof(FumenVisualEditorViewModel.BrushMode));
                editor.ToastNotify($"{Resources.BrushMode}{(editor.BrushMode ? Resources.Enable : Resources.Disable)}");
            }

            return TaskUtility.Completed;
        }
    }
}