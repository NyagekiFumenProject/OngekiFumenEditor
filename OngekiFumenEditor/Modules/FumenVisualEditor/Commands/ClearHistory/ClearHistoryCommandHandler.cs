using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ClearHistory
{
	[CommandHandler]
	public class ClearHistoryCommandHandler : CommandHandlerBase<ClearHistoryCommandDefinition>
	{
		private readonly IEditorDocumentManager editorDocumentManager;

		[ImportingConstructor]
		public ClearHistoryCommandHandler(IEditorDocumentManager editorDocumentManager)
		{
			this.editorDocumentManager = editorDocumentManager;
		}

		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = editorDocumentManager.CurrentActivatedEditor?.UndoRedoManager?.ActionStack?.Any() ?? false;
		}

		public override Task Run(Command command)
		{
			if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
			{
				var undoMgr = editor.UndoRedoManager;
				undoMgr.Clear();
			}
			return TaskUtility.Completed;
		}
	}
}