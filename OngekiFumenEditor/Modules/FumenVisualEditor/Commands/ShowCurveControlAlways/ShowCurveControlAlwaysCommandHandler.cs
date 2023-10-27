using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways
{
	[CommandHandler]
	public class ShowCurveControlAlwaysCommandHandler : CommandHandlerBase<ShowCurveControlAlwaysCommandDefinition>
	{
		private readonly IEditorDocumentManager editorDocumentManager;

		[ImportingConstructor]
		public ShowCurveControlAlwaysCommandHandler(IEditorDocumentManager editorDocumentManager)
		{
			this.editorDocumentManager = editorDocumentManager;
		}

		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
			command.Checked = editorDocumentManager.CurrentActivatedEditor?.IsShowCurveControlAlways ?? false;
		}

		public override Task Run(Command command)
		{
			if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
				editor.IsShowCurveControlAlways = !editor.IsShowCurveControlAlways;
			return TaskUtility.Completed;
		}
	}
}