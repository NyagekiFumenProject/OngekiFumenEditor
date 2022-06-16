using System;
using System.ComponentModel.Composition;
using System.Text.Json;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.About
{
    [CommandHandler]
    public class AboutCommandHandler : CommandHandlerBase<AboutCommandDefinition>
    {
        public override Task Run(Command command)
        {
            new AboutWindow().ShowDialog();
            return TaskUtility.Completed;
        }
    }
}