using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using Mono.Cecil;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg
{
    [CommandHandler]
    public class GenerateSvgCommandHandler : CommandHandlerBase<GenerateSvgCommandDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public GenerateSvgCommandHandler(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = editorDocumentManager.CurrentActivatedEditor is not null;
        }

        public override async Task Run(Command command)
        {
            if (editorDocumentManager.CurrentActivatedEditor is FumenVisualEditorViewModel editor)
            {
                try
                {
                    var opt = new SvgGenerateOption()
                    {
                        Duration = editor.EditorProjectData.AudioDuration,
                        OutputFilePath = Path.GetTempFileName() + ".svg"
                    };
                    await IoC.Get<IPreviewSvgGenerator>().GenerateSvgAsync(editor.Fumen, opt);
                    if (MessageBox.Show(Resources.GenerateSvgSuccessAndAskIfOpen, string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        ProcessUtils.OpenPath(opt.OutputFilePath);

                }
                catch (Exception e)
                {
                    MessageBox.Show(Resources.CallGenerateSvgAsyncFail + e.Message);
                }
            }
        }
    }
}