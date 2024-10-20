using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Microsoft.Xaml.Behaviors;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;

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
                editor.BrushMode = !editor.BrushMode;
                
                if (editor.BrushMode) {
                    Interaction.GetBehaviors((FumenVisualEditorView)editor.GetView()).Add(editor.BrushModeBehavior);
                }
                else {
                    var behaviors = Interaction.GetBehaviors((FumenVisualEditorView)editor.GetView());
                    behaviors.Remove(editor.BrushModeBehavior);
                }
            }

            return TaskUtility.Completed;
        }
    }
}