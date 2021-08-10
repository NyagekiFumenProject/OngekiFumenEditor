using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.TextEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.TextEditor.Commands
{
    [CommandHandler]
    public class ReloadTextEditorViewerCommandHandler : CommandHandlerBase<ReloadTextEditorViewerCommandDefinition>
    {
        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = IoC.Get<IShell>()?.ActiveItem is PersistedDocument textEditor && File.Exists(textEditor.FilePath);
        }

        public override async Task Run(Command command)
        {
            if (IoC.Get<IShell>()?.ActiveItem is PersistedDocument textEditor)
            {
                await textEditor.Load(textEditor.FilePath);
            }
        }
    }
}
