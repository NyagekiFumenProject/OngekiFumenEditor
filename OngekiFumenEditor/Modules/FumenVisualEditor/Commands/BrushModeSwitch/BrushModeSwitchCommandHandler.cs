using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

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
			if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
				editor.BrushMode = !editor.BrushMode;
			return TaskUtility.Completed;
		}
	}
}