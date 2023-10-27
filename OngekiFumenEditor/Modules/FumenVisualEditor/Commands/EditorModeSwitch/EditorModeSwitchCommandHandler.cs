using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

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
			command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
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