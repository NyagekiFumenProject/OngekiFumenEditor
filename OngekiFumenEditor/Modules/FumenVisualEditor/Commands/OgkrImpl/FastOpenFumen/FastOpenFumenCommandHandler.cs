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
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Ogkr;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen
{
    [CommandHandler]
    public class FastOpenFumenCommandHandler : CommandHandlerBase<FastOpenFumenCommandDefinition>
    {
        public override async Task Run(Command command)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = FileDialogHelper.BuildExtensionFilter((".ogkr", "音击谱面"), (".nyageki", "音击谱面"));
            openFileDialog.Title = "快速打开音击谱面";
            openFileDialog.CheckFileExists = true;

            if (openFileDialog.ShowDialog() != true)
                return;

            var ogkrFilePath = openFileDialog.FileName;
            await DocumentOpenHelper.TryOpenOgkrFileAsDocument(ogkrFilePath);
        }
    }
}