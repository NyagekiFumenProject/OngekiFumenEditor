using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using OngekiFumenEditor.Modules.FumenConverter;
using OngekiFumenEditor.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using Expression = System.Linq.Expressions.Expression;
using OngekiFumenEditor.Kernel.CommandExecutor.Attributes;
using OngekiFumenEditor.UI.Dialogs;

namespace OngekiFumenEditor.Kernel.ArgProcesser
{
    [Export(typeof(IProgramArgProcessManager))]
    internal class DefaultArgProcessManager : IProgramArgProcessManager
    {
        public async Task ProcessArgs(string[] args)
        {
            if (args.Length == 0)
                return;

            //if args[0] is openable file likes .ogkr/.nyagekiProj/.nyageki ...
            if (args.IsOnlyOne(out var filePath))
            {
                if (File.Exists(filePath))
                {
                    Log.LogInfo($"arg.filePath: {filePath}");

                    _ = Application.Current.Dispatcher.Invoke(async () =>
                    {
                        if (await DocumentOpenHelper.TryOpenAsDocument(filePath))
                            Application.Current?.MainWindow?.Focus();
                    });
                }
            }

            if (args.Contains("--notifySucess", StringComparer.InvariantCultureIgnoreCase))
            {
                Version sourceVersion = default;
                for (int i = 0; i < args.Length; i++)
                {
                    if ("--sourceVersion".Equals(args[i], StringComparison.InvariantCultureIgnoreCase) && Version.TryParse(args.ElementAtOrDefault(i + 1), out var sv))
                        sourceVersion = sv;
                }
                Application.Current.Dispatcher.Invoke(async () =>
                {
                    //wait for styles/resources have been loaded.
                    while (Application.Current.MainWindow is not Window mainWindow)
                        await Task.Delay(100);
                    new AboutWindow(true, sourceVersion).Show();
                }).NoWait();
            }
        }
    }
}
