﻿using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Kernel.EditorLayout;
using OngekiFumenEditor.Kernel.EditorLayout.Commands.About;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.UI.Dialogs;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.About
{
    [CommandHandler]
    public class ApplySuggestEditorLayoutCommandHandler : CommandHandlerBase<ApplySuggestEditorLayoutCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            await IoC.Get<IEditorLayoutManager>().ApplyDefaultSuggestEditorLayout();
        }
    }
}