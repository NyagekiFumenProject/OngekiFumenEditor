using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight
{
    [CommandHandler]
    public class RecalculateTotalHeightCommandHandler : CommandHandlerBase<RecalculateTotalHeightCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public RecalculateTotalHeightCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = editorDocumentManager.CurrentActivatedEditor?.AudioPlayer is not null;
        }

        public override Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor && editor.AudioPlayer is IAudioPlayer audio && editor.EditorProjectData is EditorProjectDataModel proj)
            {
                proj.AudioDuration = audio.Duration;
                editor.RecalculateTotalDurationHeight();

                editor.ToastNotify(Resources.RecaculatedSuccess);
            }

            return TaskUtility.Completed;
        }
    }
}