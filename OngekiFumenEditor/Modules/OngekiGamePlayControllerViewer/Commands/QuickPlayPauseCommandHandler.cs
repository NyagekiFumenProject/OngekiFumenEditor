using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using Microsoft.Win32;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Core.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Base;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Commands.QuickPlayPause
{
    [CommandHandler]
    public class QuickPlayPauseCommandHandler : CommandHandlerBase<QuickPlayPauseCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            var controller = IoC.Get<IOngekiGamePlayControllerViewer>();

            if (await controller.IsPlaying())
                await controller.Pause();
            else
                await controller.Play();
        }
    }
}