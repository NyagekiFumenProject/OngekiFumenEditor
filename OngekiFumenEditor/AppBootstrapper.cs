using Caliburn.Micro;
using Gemini.Framework.Results;
using Gemini.Framework.Services;
using Gemini.Modules.Output;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.ArgProcesser;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.DeadHandler;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
        public override bool IsPublishSingleFileHandled => true;

        public AppBootstrapper() : this(true)
        {
        }

        public AppBootstrapper(bool useApplication = true) : base(useApplication)
        {
        }

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
            DumpFileHelper.Init();
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
            return base.SelectAssemblies()
                .Append(typeof(IOutput).Assembly)
                .Append(typeof(IMainWindow).Assembly)
                .Distinct();
        }

        protected void LogBaseInfos()
        {
            Log.LogInfo($"Application verison : {GetType().Assembly.GetName().Version} , Product Version+CommitHash : {FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).ProductVersion}");
            Log.LogInfo($"User CurrentCulture: {CultureInfo.CurrentCulture}, CurrentUICulture: {CultureInfo.CurrentUICulture}, DefaultThreadCurrentCulture: {CultureInfo.DefaultThreadCurrentCulture}, DefaultThreadCurrentUICulture: {CultureInfo.DefaultThreadCurrentUICulture}");
        }

        protected async override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);
            InitExceptionCatcher();
            LogBaseInfos();

            IoC.Get<IShell>().ToolBars.Visible = true;
            IoC.Get<WindowTitleHelper>().TitleContent = "";

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/logo32.ico");
            logo.EndInit();
            IoC.Get<WindowTitleHelper>().Icon = logo;

            await InitKernels();
            await IoC.Get<IProgramArgProcessManager>().ProcessArgs(e.Args);

            if (ProgramSetting.Default.UpgradeProcessPriority)
            {
                var curProc = Process.GetCurrentProcess();
                //提升
                var before = curProc.PriorityClass;
                var after = ProcessPriorityClass.High;
                curProc.PriorityClass = after;
                Log.LogDebug($"Upgrade process priority: {before} -> {after}");

                curProc.PriorityBoostEnabled = true;
            }

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");

            if (Application.MainWindow is Window window)
            {
                window.AllowDrop = true;
                window.Drop += MainWindow_Drop;
            }

            /**/
            var bpmList = new BpmList();
            var soflanList = new SoflanList(new Soflan[] {
                new (){ TGrid = new(1,0),EndTGrid = new(2,0),Speed = -1 },
                new (){ TGrid = new(2,0),EndTGrid = new(3,0),Speed = 2 }
            });

            var designList = soflanList.GetCachedSoflanPositionList_DesignMode(240, bpmList);
            var previewList = soflanList.GetCachedSoflanPositionList_PreviewMode(240, bpmList);

            var y = TGridCalculator.ConvertTGridToY_PreviewMode(new(1, 0), soflanList, bpmList, 1, 240);
        }

        private async void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length == 1)
                {
                    var filePath = files[0];
                    await DocumentOpenHelper.TryOpenAsDocument(filePath);
                }

                e.Handled = true;
                return;
            }

            e.Handled = false;
        }

        private bool exceptionHandling = false;

        private void InitExceptionCatcher()
        {
            var recHandle = new HashSet<IntPtr>();

            void LogException(object sender, Exception exception)
            {
                if (exceptionHandling)
                    return;
                exceptionHandling = true;

                var sb = new StringBuilder();
                void exceptionDump(Exception e, int level = 0)
                {
                    if (e is null)
                        return;
                    var tab = string.Concat("\t".Repeat(2 * level));

                    sb.AppendLine();
                    sb.AppendLine(tab + $"Exception lv.{level} : {e.Message}");
                    sb.AppendLine(tab + $"Stack : {e.StackTrace}");

                    exceptionDump(e.InnerException, level + 1);
                }
                sb.AppendLine($"----------Exception Catcher----------");
                sb.AppendLine($"Program notice a (unhandled) exception from object: {sender}({sender?.GetType().FullName})");
                exceptionDump(exception);
                sb.AppendLine($"----------------------------");
                FileLogOutput.WriteLog(sb.ToString());
                FileLogOutput.WaitForWriteDone();
#if !DEBUG
                var exceptionHandle = Marshal.GetExceptionPointers();
                if (exceptionHandle != IntPtr.Zero && !recHandle.Contains(exceptionHandle))
                {
                    DumpFileHelper.WriteMiniDump(exceptionHandle);
                    recHandle.Add(exceptionHandle);
                }

                FileLogOutput.WriteLog("FumenRescue.Rescue() Begin");
                FumenRescue.Rescue();
                FileLogOutput.WriteLog("FumenRescue.Rescue() End");
                FileLogOutput.WaitForWriteDone();
#endif
                exceptionHandling = true;
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, e) => LogException(sender, e.ExceptionObject as Exception);
            Application.Current.DispatcherUnhandledException += (sender, e) => LogException(sender, e.Exception);
            TaskScheduler.UnobservedTaskException += (sender, e) => LogException(sender, e.Exception);
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
