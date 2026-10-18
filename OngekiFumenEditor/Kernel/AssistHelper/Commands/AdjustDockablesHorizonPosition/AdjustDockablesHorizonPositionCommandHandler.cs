using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.AssistHelper.Impls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
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
            if (EditorDocumentManager.CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
                return TaskUtility.Completed;

            var adjustments = AdjustDockablesHorizonPositionHelper.CalculateAdjustments(editor.Fumen);
            if (adjustments.Count == 0)
                return TaskUtility.Completed;

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.CommandAdjustDockablesHorizonPosition, () =>
            {
                foreach (var adjustment in adjustments)
                    adjustment.Target.XGrid = adjustment.After.CopyNew();
            }, () =>
            {
                foreach (var adjustment in adjustments)
                    adjustment.Target.XGrid = adjustment.Before.CopyNew();
            }));

            return TaskUtility.Completed;
        }
    }
}
