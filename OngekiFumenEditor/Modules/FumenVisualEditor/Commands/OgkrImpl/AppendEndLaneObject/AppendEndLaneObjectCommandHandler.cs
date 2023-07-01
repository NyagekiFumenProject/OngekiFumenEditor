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

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.AppendEndLaneObject
{
    [CommandHandler]
    public class AppendEndLaneObjectCommandHandler : CommandHandlerBase<AppendEndLaneObjectCommandDefinition>
    {
        public override void Update(Command command)
        {
            base.Update(command);
            command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
        }

        public override Task Run(Command command)
        {
            var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
            var fumen = editor.Fumen;

            var targetStartObjs = fumen.Lanes.Where(x => x.Children.LastOrDefault() is not ConnectableEndObject).ToArray();

            foreach (var startObj in targetStartObjs)
            {
                var lastObj = startObj.Children.LastOrDefault() ?? startObj as ConnectableObjectBase;
                var endObj = startObj.CreateEndObject();

                endObj.TGrid = lastObj.TGrid;
                endObj.XGrid = lastObj.XGrid;

                startObj.AddChildObject(endObj);
            }

            editor.Toast.ShowMessage($"已补上 {targetStartObjs.Length} 个轨道物件");

            return Task.CompletedTask;
        }
    }
}