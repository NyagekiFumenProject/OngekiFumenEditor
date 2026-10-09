using Caliburn.Micro;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FumenStatistics
{
    [CommandHandler]
    public class FumenStatisticsCommandHandler : CommandHandlerBase<FumenStatisticsCommandDefinition>
    {
        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor?.Fumen is not null;
        }

        public override async Task Run(Command command)
        {
            var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
            if (editor?.Fumen is not { } fumen)
                return;

            await IoC.Get<IWindowManager>().ShowDialogAsync(new FumenStatisticsDialogViewModel(fumen, editor.DisplayName));
        }
    }
}
