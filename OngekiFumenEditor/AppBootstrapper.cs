using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.ArgProcesser;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.LocatorOverride;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OngekiFumenEditor
{
    public class AppBootstrapper : Gemini.AppBootstrapper
    {
        protected async Task InitKernels()
        {
            await IoC.Get<ISchedulerManager>().Init();

            //overwrite ViewLocator
            var locateForModel = ViewLocator.LocateForModel;
            ViewLocator.LocateForModel = (model, hostControl, ctx) =>
            {
                var r = locateForModel(model, hostControl, ctx);
                if (r is not null)
                    if (r is not TextBlock t || !t.Text.StartsWith("Cannot find"))
                        return r;
                return ViewHelper.CreateView(model);
            };
            foreach (var locatorOverrider in IoC.GetAll<ILocatorOverride>())
                locatorOverrider.Override();
        }

        protected override void BindServices(CompositionBatch batch)
        {
            base.BindServices(batch);

            //setup Pluigins
            var exePath = Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath);
            var pluginsDirPath = Path.Combine(exeDir, "Plugins");
            Directory.CreateDirectory(pluginsDirPath);
            var pluginsDirPaths = Directory.EnumerateDirectories(pluginsDirPath);

            foreach (var path in pluginsDirPaths)
            {
                Debug.WriteLine($"----------------");
                Debug.WriteLine($"加载插件子目录:{path}");
                try
                {
                    var directoryCatalog = new DirectoryCatalog(path);
                    foreach (var partDef in directoryCatalog.Parts)
                    {
                        var part = partDef.CreatePart();
                        batch.AddPart(part);
                        var imports = part.ToString();
                        var exports = string.Join(", ", part.ExportDefinitions.Select(x => x.ContractName));
                        Debug.WriteLine($"Export ({imports}) => ({exports})");
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"加载插件子目录出错:{e.Message}");
                }
                Debug.WriteLine($"----------------");
            }
        }

        protected override void Configure()
        {
            FileLogOutput.Init();
            base.Configure();
            var defaultCreateTrigger = Caliburn.Micro.Parser.CreateTrigger;

            Caliburn.Micro.Parser.CreateTrigger = (target, triggerText) =>
            {
                if (triggerText == null)
                {
                    return defaultCreateTrigger(target, null);
                }

                var triggerDetail = triggerText
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty);

                var splits = triggerDetail.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                switch (splits[0])
                {
                    case "Key":
                        var key = (Key)Enum.Parse(typeof(Key), splits[1], true);
                        return new KeyTrigger { Key = key };

                    case "Gesture":
                        var mkg = (MultiKeyGesture)(new MultiKeyGestureConverter()).ConvertFrom(splits[1]);
                        return new KeyTrigger { Modifiers = mkg.KeySequences[0].Modifiers, Key = mkg.KeySequences[0].Keys[0] };
                }

                return defaultCreateTrigger(target, triggerText);
            };
        }

        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            return base.SelectAssemblies();
        }

        protected async override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);
            Log.LogInfo($"Application verison : {GetType().Assembly.GetName().Version} , Product Version+CommitHash : {FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).ProductVersion}");

            IoC.Get<IShell>().ToolBars.Visible = true;
            IoC.Get<WindowTitleHelper>().TitleContent = "";

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/logo.png");
            logo.EndInit();
            IoC.Get<WindowTitleHelper>().Icon = logo;

            //cultureInfo
            Log.LogInfo($"User current CultureInfo : {CultureInfo.CurrentCulture} , UI CultureInfo : {CultureInfo.CurrentUICulture}");

            await InitKernels();

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");

            await IoC.Get<IProgramArgProcessManager>().ProcessArgs(e.Args);
        }

        protected override async void OnExit(object sender, EventArgs e)
        {
            IoC.Get<IAudioManager>().Dispose();
            await IoC.Get<ISchedulerManager>().Term();
            FileLogOutput.WriteLog("\n----------CLOSE FILE LOG OUTPUT----------");
            FileLogOutput.Term();
            base.OnExit(sender, e);
        }
    }
}
