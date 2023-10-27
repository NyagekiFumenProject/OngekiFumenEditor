using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.AssistHelper.Impls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands.AdjustDockablesHorizonPosition
{
	[CommandHandler]
	public class AdjustDockablesHorizonPositionCommandHandler : CommandHandlerBase<AdjustDockablesHorizonPositionCommandDefinition>
	{
		[Import(typeof(IEditorDocumentManager))]
		public IEditorDocumentManager EditorDocumentManager { get; set; }

		public override void Update(Command command)
		{
			command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
			base.Update(command);
		}

		public override Task Run(Command command)
		{
			AdjustDockablesHorizonPositionHelper.Execute(EditorDocumentManager.CurrentActivatedEditor.Fumen);
			return TaskUtility.Completed;
		}
	}
}