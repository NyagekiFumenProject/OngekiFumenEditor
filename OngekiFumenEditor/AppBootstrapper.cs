using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using Gemini.Framework.Services;
using Gemini.Modules.Output;
using OngekiFumenEditor.Kernel.ArgProcesser;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.CommandExecutor;
using OngekiFumenEditor.Kernel.EditorLayout;
using OngekiFumenEditor.Kernel.ProgramUpdater;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.SplashScreen;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.DeadHandler;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using SevenZip.Compression.LZ;
#if !DEBUG
using System.Runtime.InteropServices;
using System.Threading;
using MahApps.Metro.Controls;
using OngekiFumenEditor.UI.Dialogs;
#endif

namespace OngekiFumenEditor;

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

    private bool? isGUIMode = null;
    public bool IsGUIMode
    {
        get => isGUIMode ?? ((App.Current as App)?.IsGUIMode ?? false);
        set => isGUIMode = value;
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
            Debug.WriteLine("----------------");
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

            var pluginsName = Path.GetFileName(path);
            if (pluginsName.StartsWith("OngekiFumenEditorPlugins."))
            {
                var pluginDllAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(x => x.GetName().Name == pluginsName).FirstOrDefault();
                if (pluginDllAssembly != null)
                {
                    AssemblySource.AddRange(new[] { pluginDllAssembly });
                    Debug.WriteLine($"Add plugin assembly {pluginDllAssembly.GetName().Name}.dll into AssemblySource");
                }
            }

            Debug.WriteLine("----------------");
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
                return defaultCreateTrigger(target, null);

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
                    var mkg = (MultiKeyGesture)new MultiKeyGestureConverter().ConvertFrom(splits[1]);
                    return new KeyTrigger
                    { Modifiers = mkg.KeySequences[0].Modifiers, Key = mkg.KeySequences[0].Keys[0] };
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
        Log.LogInfo(
            $"Application verison : {GetType().Assembly.GetName().Version} , Product Version+CommitHash : {FileVersionInfo.GetVersionInfo(GetType().Assembly.Location).ProductVersion}");
        Log.LogInfo(
            $"User CurrentCulture: {CultureInfo.CurrentCulture}, CurrentUICulture: {CultureInfo.CurrentUICulture}, DefaultThreadCurrentCulture: {CultureInfo.DefaultThreadCurrentCulture}, DefaultThreadCurrentUICulture: {CultureInfo.DefaultThreadCurrentUICulture}");
    }

    private bool CheckIfAdminPermission()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    protected override void OnStartup(object sender, StartupEventArgs e)
    {
        var isGUIMode = (App.Current as App)?.IsGUIMode ?? false;

        if (isGUIMode)
        {
            OnStartupForGUI(sender, e);
        }
        else
        {
            OnStartupForCMD(sender, e);
        }
    }

    public async void OnStartupForCMD(object sender, StartupEventArgs e)
    {
        IsGUIMode = false;
        Log.Instance.RemoveOutput<ConsoleLogOutput>();

        await IoC.Get<ISchedulerManager>().Init();

        var executor = IoC.Get<ICommandExecutor>();

        try
        {
            Application.Current.Shutdown(await executor.Execute(e.Args));
        }
        catch (Exception ex)
        {
            Log.LogError($"Unhandled exception processing arguments:\n{ex.Message}");
            Application.Current.Shutdown(1);
        }
    }

    public async void OnStartupForGUI(object sender, StartupEventArgs e)
    {
        IsGUIMode = true;

#if DEBUG
        ConsoleWindowHelper.SetConsoleWindowVisible(true);
#else
        ConsoleWindowHelper.SetConsoleWindowVisible(ProgramSetting.Default.ShowConsoleWindowInGUIMode);
#endif

        InitExceptionCatcher();
        LogBaseInfos();
        InitIPCServer();

        await IoC.Get<ISchedulerManager>().Init();

        try
        {
            //process command args
            await IoC.Get<IProgramArgProcessManager>().ProcessArgs(e.Args);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Unhandled exception processing arguments:\n{ex.Message}");
            Application.Current.Shutdown(-1);
            return;
        }

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
            var prevSuffix = IoC.Get<WindowTitleHelper>().TitleSuffix;
            IoC.Get<WindowTitleHelper>().TitleSuffix = prevSuffix + "(以管理员权限运行)";
        }
        IoC.Get<WindowTitleHelper>().UpdateWindowTitle();

        IoC.Get<IShell>().ToolBars.Visible = true;

        var logo = new BitmapImage();
        logo.BeginInit();
        logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/logo32.ico");
        logo.EndInit();
        IoC.Get<WindowTitleHelper>().Icon = logo;

        Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");

        await DisplayRootViewForAsync<IMainWindow>();

        if (Application.MainWindow is Window window)
        {
            window.AllowDrop = true;
            window.Drop += MainWindow_Drop;

            //program will forget position/size when it has been called as commandline.
            //so we have to remember and restore windows' position/size manually.
            window.Closed += MainWindow_Closed;
            if (!string.IsNullOrWhiteSpace(ProgramSetting.Default.WindowSizePositionLastTime))
            {
                var arr = ProgramSetting.Default.WindowSizePositionLastTime.Split(",").Select(x => double.Parse(x.Trim())).ToArray();
                window.Left = arr[0];
                window.Top = arr[1];
                window.Width = arr[2];
                window.Height = arr[3];
            }
        }

        var showSplashWindow = IoC.Get<IShell>().Documents.IsEmpty() &&
                               !ProgramSetting.Default.DisableShowSplashScreenAfterBoot;
        if (showSplashWindow)
            await IoC.Get<IWindowManager>().ShowWindowAsync(IoC.Get<ISplashScreenWindow>());

        if (ProgramSetting.Default.IsFirstTimeOpenEditor)
        {
            if (MessageBox.Show(Resources.ShouldLoadSuggestLayout, Resources.Suggest, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var result = await IoC.Get<IEditorLayoutManager>().ApplyDefaultSuggestEditorLayout();
                if (!result)
                    MessageBox.Show(Resources.LoadLayoutFailed);
            }

            ProgramSetting.Default.IsFirstTimeOpenEditor = false;
            ProgramSetting.Default.Save();
        }

        IoC.Get<IProgramUpdater>().CheckUpdatable().NoWait();
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        if (sender is not Window mainWindow)
            return;

        ProgramSetting.Default.WindowSizePositionLastTime = string.Join(", ", new[] {
            mainWindow.Left,
            mainWindow.Top,
            mainWindow.Width,
            mainWindow.Height
        });
        ProgramSetting.Default.Save();
        Log.LogInfo($"WindowSizePositionLastTime = {ProgramSetting.Default.WindowSizePositionLastTime}");

        App.Current.Shutdown();
    }

    private void InitIPCServer()
    {
        //if (ProgramSetting.Default.EnableMultiInstances)
        //    return;
        ipcThread = new AbortableThread(cancelToken =>
        {
            while (!cancelToken.IsCancellationRequested)
            {
                if (!IPCHelper.IsSelfHost())
                {
                    //如果自己不是host那就检查另一个host死了没,来个随机sleep那样的话可以避免多个实例撞车
                    Thread.Sleep(MathUtils.Random(0, 1000));
                    if (!IPCHelper.IsHostAlive())
                    {
                        //似了就继承大业
                        IPCHelper.SetSelfHost();
                        Log.LogDebug("Current application instance is IPC host now.");
                    }
                }

                try
                {
                    var line = IPCHelper.ReadLine(cancelToken)?.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    Log.LogDebug($"Recv line by IPC:{line}");
                    if (line.StartsWith("CMD:"))
                    {
                        var args = JsonSerializer.Deserialize<IPCHelper.ArgsWrapper>(line[4..]).Args;
                        Application.Current.Dispatcher.Invoke(() =>
                            IoC.Get<IProgramArgProcessManager>().ProcessArgs(args));
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarn($"Recv line by IPC throw exception:{e.Message}");
                }
            }
        })
        {
            Name = "OngekiFumenEditorIPCThread"
        };
        ipcThread.Start();
    }

    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
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

    private bool exceptionHandling;
    private AbortableThread ipcThread;

    private void InitExceptionCatcher()
    {
        var recHandle = new HashSet<IntPtr>();

        async void ProcessException(object sender, Exception exception, string trigSource)
        {
            if (exceptionHandling)
                return;
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

            sb.AppendLine("----------Exception Catcher----------");
            sb.AppendLine(
                $"Program notice a (unhandled) exception from object: {sender}({sender?.GetType().FullName})");
            exceptionDump(exception);
            sb.AppendLine("----------------------------");
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