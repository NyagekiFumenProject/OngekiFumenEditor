using Caliburn.Micro;
using ControlzEx.Standard;
using Gemini.Framework.Services;
using Gemini.Modules.Output;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.ArgProcesser;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.SplashScreen;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
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
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OngekiFumenEditor
{
    public class AppBootstrapper : Gemini.AppBootstrapper
    {
#if !DEBUG
		public override bool IsPublishSingleFileHandled => true;
#endif

        public AppBootstrapper() : this(true)
        {
        }

        public AppBootstrapper(bool useApplication = true) : base(useApplication)
        {

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

        private bool CheckIfAdminPermission()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        protected async override void OnStartup(object sender, StartupEventArgs e)
        {
            InitExceptionCatcher();
            LogBaseInfos();
            InitIPCServer();

            await IoC.Get<ISchedulerManager>().Init();
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

            ShowStartupGUI();
        }

        private void OnStartupForGUI()
        {
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

            if (CheckIfAdminPermission())
            {
                Log.LogWarn("Program is within admin permission.");
                IoC.Get<WindowTitleHelper>().TitleContent = "(以管理员权限运行)";
            }
            else
                IoC.Get<WindowTitleHelper>().TitleContent = "";

            IoC.Get<IShell>().ToolBars.Visible = true;

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/logo32.ico");
            logo.EndInit();
            IoC.Get<WindowTitleHelper>().Icon = logo;

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");

            if (Application.MainWindow is Window window)
            {
                window.AllowDrop = true;
                window.Drop += MainWindow_Drop;
            }
        }

        public async void ShowStartupGUI()
        {
            OnStartupForGUI();

            await DisplayRootViewFor<IMainWindow>();
            var showSplashWindow = IoC.Get<IShell>().Documents.IsEmpty() && !ProgramSetting.Default.DisableShowSplashScreenAfterBoot;
            if (showSplashWindow)
                await IoC.Get<IWindowManager>().ShowWindowAsync(IoC.Get<ISplashScreenWindow>());
        }

        private void InitIPCServer()
        {
            //if (ProgramSetting.Default.EnableMultiInstances)
            //    return;
            ipcThread = new AbortableThread(async (cancelToken) =>
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    if (!IPCHelper.IsSelfHost())
                    {
                        //如果自己不是host那就检查另一个host死了没,来个随机sleep那样的话可以避免多个实例撞车
                        await Task.Delay(MathUtils.Random(0, 1000));
                        if (!IPCHelper.IsHostAlive())
                        {
                            //似了就继承大业
                            IPCHelper.SetSelfHost();
                            Log.LogDebug("Current application instance is IPC host now.");
                        }
                    }

                    try
                    {
                        var line = IPCHelper.ReadLineAsync(cancelToken)?.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        Log.LogDebug($"Recv line by IPC:{line}");
                        if (line.StartsWith("CMD:"))
                        {
                            var args = JsonSerializer.Deserialize<IPCHelper.ArgsWrapper>(line[4..]).Args;
                            await Application.Current.Dispatcher.InvokeAsync(() => IoC.Get<IProgramArgProcessManager>().ProcessArgs(args));
                        }
                    }
                    catch (Exception e)
                    {
                        Log.LogWarn($"Recv line by IPC throw exception:{e.Message}");
                    }
                }
            })
            {
                Name = "OngekiFumenEditorIPCThread",
            };
            ipcThread.Start();
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
        private AbortableThread ipcThread;

        private void InitExceptionCatcher()
        {
            var recHandle = new HashSet<IntPtr>();

            async void ProcessException(object sender, Exception exception, string trigSource)
            {
                if (exceptionHandling)
                {
                    return;
                }
                exceptionHandling = true;

                await FileLogOutput.WriteLog($"trigged by {trigSource}");

                try
                {
                    foreach (var visual in Application.Current.Windows.OfType<Window>())
                        visual.Hide();
                    IoC.Get<IAudioPlayerToolViewer>()?.AudioPlayer?.Pause();
                }
                catch
                {

                }

                var innerMessage = exception.Message;

                var sb = new StringBuilder();
                void exceptionDump(Exception e, int level = 0)
                {
                    if (e is null)
                        return;
                    var tab = string.Concat("\t".Repeat(2 * level));

                    innerMessage = e.Message;

                    sb.AppendLine();
                    sb.AppendLine(tab + $"Exception lv.{level} : {e.Message}");
                    sb.AppendLine(tab + $"Stack : {e.StackTrace}");

                    exceptionDump(e.InnerException, level + 1);
                }
                sb.AppendLine($"----------Exception Catcher----------");
                sb.AppendLine($"Program notice a (unhandled) exception from object: {sender}({sender?.GetType().FullName})");
                exceptionDump(exception);
                sb.AppendLine($"----------------------------");
                await FileLogOutput.WriteLog(sb.ToString());
#if !DEBUG
                var exceptionHandle = Marshal.GetExceptionPointers();
                var dumpFile = string.Empty;
                if (exceptionHandle != IntPtr.Zero && !recHandle.Contains(exceptionHandle))
                {
                    dumpFile = DumpFileHelper.WriteMiniDump(exceptionHandle);
                    recHandle.Add(exceptionHandle);
                }
                await FileLogOutput.WriteLog("FumenRescue.Rescue() Begin\n");
                var resuceFolders = await FumenRescue.Rescue();
                await FileLogOutput.WriteLog("FumenRescue.Rescue() End\n");

                var logFile = FileLogOutput.GetCurrentLogFile();

                var apartmentState = Thread.CurrentThread.GetApartmentState();
                await FileLogOutput.WriteLog($"current apartmentState: {apartmentState}\n");
                if (apartmentState == ApartmentState.STA)
                {
                    var exceptionWindow = new ExceptionTermWindow(innerMessage, resuceFolders, logFile, dumpFile);
                    exceptionWindow.ShowDialog();
                }
                else
                {
                    var result = Application.Current.Invoke(() =>
                    {
                        var exceptionWindow = new ExceptionTermWindow(innerMessage, resuceFolders, logFile, dumpFile);
                        return exceptionWindow.ShowDialog();
                    });
                }

                exceptionHandling = true;
                Environment.Exit(-1);
#else
                throw exception;
#endif
            }

            AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
            {
                ProcessException(sender, e.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
            };
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                ProcessException(sender, e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                ProcessException(sender, e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        protected override async void OnExit(object sender, EventArgs e)
        {
            ipcThread?.Abort();
            IoC.Get<IAudioManager>().Dispose();
            await IoC.Get<ISchedulerManager>().Term();
            FileLogOutput.WriteLog("\n----------CLOSE FILE LOG OUTPUT----------");
            base.OnExit(sender, e);
        }
    }
}
