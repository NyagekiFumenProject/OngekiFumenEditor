using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.FastPlayPause
{
	[CommandHandler]
	public class FastPlayPauseCommandHandler : CommandHandlerBase<FastPlayPauseCommandDefinition>
	{
		private IEditorDocumentManager editorDocumentManager;

		[ImportingConstructor]
		public FastPlayPauseCommandHandler(IEditorDocumentManager editorDocumentManager)
		{
			this.editorDocumentManager = editorDocumentManager;
		}

		public override void Update(Command command)
		{
			base.Update(command);

			command.Enabled = editorDocumentManager?.CurrentActivatedEditor is not null;
		}

		public override Task Run(Command command)
		{
			editorDocumentManager?.CurrentActivatedEditor?.KeyboardAction_PlayOrPause();
			return TaskUtility.Completed;
		}
	}
}