using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
using MahApps.Metro.Controls;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OngekiFumenEditor.Kernel.ArgProcesser;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.CommandExecutor;
using OngekiFumenEditor.Kernel.EditorLayout;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Mcp;
using OngekiFumenEditor.Kernel.ProgramUpdater;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.SplashScreen;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.DeadHandler;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;

namespace OngekiFumenEditor;

public class AppBootstrapper : Gemini.AppBootstrapper
{
    /*
    #region Assembly Force Reference Holders
    //Costura won't pack some assembly because of this assembly not reference their assembly directly.
    //If you throw FileNotFoundException for some .dll files when you using CLI, just check here.
#pragma warning disable
    private object __forcePackAssemblyHolders = new[] { typeof(Microsoft.Extensions.ObjectPool.ObjectPool).Assembly }.Select(x => x.FullName);
#pragma warning restore
    #endregion
    */

    private sealed class EditorCoreLogTarget : OngekiFumenEditor.Utils.ICoreLogTarget
    {
        public void Write(OngekiFumenEditor.Utils.CoreLogLevel level, string message, Exception exception, string memberName, string filePath, int lineNumber)
        {
            var actualMessage = string.IsNullOrWhiteSpace(memberName) ? message : $"[{memberName}:{lineNumber}] {message}";

            switch (level)
            {
                case OngekiFumenEditor.Utils.CoreLogLevel.Debug:
                    Log.LogDebug(actualMessage);
                    break;
                case OngekiFumenEditor.Utils.CoreLogLevel.Info:
                    Log.LogInfo(actualMessage);
                    break;
                case OngekiFumenEditor.Utils.CoreLogLevel.Warn:
                    Log.LogWarn(actualMessage);
                    break;
                case OngekiFumenEditor.Utils.CoreLogLevel.Error when exception is not null:
                    Log.LogError(actualMessage, exception);
                    break;
                case OngekiFumenEditor.Utils.CoreLogLevel.Error:
                    Log.LogError(actualMessage);
                    break;
            }
        }
    }

    private static readonly OngekiFumenEditor.Utils.ICoreLogTarget coreLogTarget = new EditorCoreLogTarget();

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

    private EventWaitHandle ReadyEvent = new(false, EventResetMode.AutoReset, "OngekiFumenEditor_ReadyEvent");

    private void SetAppReady()
    {
        ReadyEvent.Set();
    }

    protected override void BindServices(CompositionBatch batch)
    {
        base.BindServices(batch);

        // Optional plugins must never make the core composition fail.  In
        // particular, Directory.CreateDirectory/EnumerateDirectories can throw
        // before a plugin catalog is even created (read-only or malformed installs).
        var exeDir = AppDirectoryHelper.ExecutableDirectory;
        var pluginsDirPath = Path.Combine(exeDir, "Plugins");
        try
        {
            Directory.CreateDirectory(pluginsDirPath);
            foreach (var path in Directory.EnumerateDirectories(pluginsDirPath))
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
                    LogOptionalStartupFailure($"加载插件子目录出错: {path}", e);
                }

                try
                {
                    var pluginsName = Path.GetFileName(path);
                    if (pluginsName.StartsWith("OngekiFumenEditorPlugins.", StringComparison.Ordinal))
                    {
                        var pluginDllAssembly = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(x => x.GetName().Name == pluginsName);
                        if (pluginDllAssembly != null)
                        {
                            AssemblySource.AddRange(new[] { pluginDllAssembly });
                            Debug.WriteLine($"Add plugin assembly {pluginDllAssembly.GetName().Name}.dll into AssemblySource");
                        }
                    }
                }
                catch (Exception e)
                {
                    LogOptionalStartupFailure($"注册插件程序集出错: {path}", e);
                }

                Debug.WriteLine("----------------");
            }
        }
        catch (Exception e)
        {
            LogOptionalStartupFailure($"跳过插件扫描: {pluginsDirPath}", e);
        }
    }

    private static void LogOptionalStartupFailure(string message, Exception exception)
    {
        Debug.WriteLine($"{message}: {exception}");
        try
        {
            Log.LogWarn($"{message}: {exception.Message}");
        }
        catch
        {
            // Composition may not have finished yet; Debug output is the fallback.
        }
    }

    protected override void Configure()
    {
        try
        {
            FileLogOutput.Init();
            DumpFileHelper.Init();
            CoreLog.SetResolver(() => coreLogTarget);
            base.Configure();
        }
        catch (Exception exception)
        {
            HandleStartupFailure(exception, "Configure");
            throw;
        }
        if (Interlocked.Exchange(ref processExitHandlerInstalled, 1) == 0)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushLogsForProcessExit();
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
            .Append(typeof(IFumenParserManager).Assembly)
            .Distinct();
    }

    protected void LogBaseInfos()
    {
        Log.LogInfo(
            $"Application Verison+CommitHash : {ThisAssembly.AssemblyInformationalVersion}");
        Log.LogInfo(
            $"AppContext.BaseDirectory : {AppContext.BaseDirectory}");
        Log.LogInfo(
            $"User CurrentCulture: {CultureInfo.CurrentCulture}, CurrentUICulture: {CultureInfo.CurrentUICulture}, DefaultThreadCurrentCulture: {CultureInfo.DefaultThreadCurrentCulture}, DefaultThreadCurrentUICulture: {CultureInfo.DefaultThreadCurrentUICulture}");
    }

    private static void HandleStartupFailure(Exception exception, string phase)
    {
        var dumpFile = string.Empty;
        try
        {
            // The dump must be attempted before any best-effort log flush or UI work.
            dumpFile = DumpFileHelper.WriteMiniDump(IntPtr.Zero) ?? string.Empty;
        }
        catch (Exception dumpException)
        {
            Debug.WriteLine($"Write startup dump failed: {dumpException}");
        }

        try
        {
            FileLogOutput.WriteLog($"Startup failed during {phase}: {exception}\nDump file: {dumpFile}").GetAwaiter().GetResult();
            FileLogOutput.WaitForWriteDone();
        }
        catch (Exception logException)
        {
            Debug.WriteLine($"Write startup failure log failed: {logException}");
        }
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
        try
        {
            InitExceptionCatcher();
            Log.Instance.RemoveOutput<ConsoleLogOutput>();
            SetAppReady();
            await IoC.Get<ISchedulerManager>().Init();

            var executor = IoC.Get<ICommandExecutor>();
            Application.Current.Shutdown(await executor.Execute(e.Args));
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex, "command-line startup");
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

        ApplyProcessPriorityTier();

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
                var values = ProgramSetting.Default.WindowSizePositionLastTime
                    .Split(',', StringSplitOptions.TrimEntries);
                if (values.Length == 4
                    && values.All(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                {
                    var arr = values.Select(x => double.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                    if (arr.All(double.IsFinite) && arr[2] > 0 && arr[3] > 0)
                    {
                        window.Left = arr[0];
                        window.Top = arr[1];
                        window.Width = arr[2];
                        window.Height = arr[3];
                    }
                    else
                    {
                        Log.LogWarn($"Ignore invalid window geometry values: {ProgramSetting.Default.WindowSizePositionLastTime}");
                    }
                }
                else
                {
                    Log.LogWarn($"Ignore malformed window geometry setting: {ProgramSetting.Default.WindowSizePositionLastTime}");
                }
            }
        }

        var showSplashWindow = IoC.Get<IShell>().Documents.IsEmpty() &&
                               !ProgramSetting.Default.DisableShowSplashScreenAfterBoot;
        if (showSplashWindow)
            await IoC.Get<IWindowManager>().ShowWindowAsync(IoC.Get<ISplashScreenWindow>());
        SetAppReady();

        await TryStartMcpServerAsync();

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

        //var fumen = await IoC.Get<IFumenParserManager>().Deserialize("F:\\refresh\\package\\option\\A016\\music\\music8185\\8185_10.ogkr");
        //fumen.IndividualSoflanAreaMap.DebugDump();
        //var isf = fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x).FirstOrDefault(x => x.Id == 2375);
        //var queryPath = fumen.IndividualSoflanAreaMap.DebugFindDataQueryPath(isf);
        //var soflanGroup = fumen.IndividualSoflanAreaMap.QuerySoflanGroup(new(-12, 0), new(10, 1440));
    }

    /// <summary>
    /// 按 <see cref="ProgramSetting.ProcessPriorityTier"/> 应用进程优先级：0=Normal(不修改)/1=BelowNormal/2=AboveNormal/3=High。
    /// 非 Windows 平台跳过；异常只记日志，不影响启动。
    /// </summary>
    private static void ApplyProcessPriorityTier()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var tier = ProgramSetting.Default.ProcessPriorityTier;

        ProcessPriorityClass priorityClass;
        switch (tier)
        {
            case 1:
                priorityClass = ProcessPriorityClass.BelowNormal;
                break;
            case 2:
                priorityClass = ProcessPriorityClass.AboveNormal;
                break;
            case 3:
                priorityClass = ProcessPriorityClass.High;
                break;
            default:
                return;
        }

        try
        {
            var curProc = Process.GetCurrentProcess();
            //提升
            var before = curProc.PriorityClass;
            curProc.PriorityClass = priorityClass;
            curProc.PriorityBoostEnabled = true;
            Log.LogDebug($"Upgrade process priority: {before} -> {priorityClass}");
        }
        catch (Exception ex)
        {
            Log.LogWarn($"Failed to apply process priority tier {tier}: {ex.Message}");
        }
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        if (sender is not Window mainWindow)
            return;

        var geometry = new[] { mainWindow.Left, mainWindow.Top, mainWindow.Width, mainWindow.Height };
        if (geometry.All(double.IsFinite) && geometry[2] > 0 && geometry[3] > 0)
        {
            ProgramSetting.Default.WindowSizePositionLastTime = string.Join(", ", geometry.Select(x =>
                x.ToString("R", CultureInfo.InvariantCulture)));
            ProgramSetting.Default.Save();
            Log.LogInfo($"WindowSizePositionLastTime = {ProgramSetting.Default.WindowSizePositionLastTime}");
        }
        else
        {
            Log.LogWarn($"Skip saving invalid window geometry: {string.Join(", ", geometry)}");
        }

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

    private int exceptionHandling;
    private AbortableThread ipcThread;
    private static int processExitHandlerInstalled;

    private static void FlushLogsForProcessExit()
    {
        try
        {
            // ProcessExit is synchronous. Drain Log's queue first, then the file sink's
            // independent batch writer, so an async OnExit continuation cannot leave records behind.
            Log.WaitForAllLogWriteDone().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Flush logs during process exit failed : {exception}");
            try
            {
                FileLogOutput.WaitForWriteDone();
            }
            catch (Exception fileException)
            {
                Debug.WriteLine($"Flush file logs during process exit failed : {fileException}");
            }
        }
    }

    private void InitExceptionCatcher()
    {
#if !DEBUG
        async Task FinishExceptionHandlingAsync(Exception exception, string exceptionDetails, string innerMessage, string dumpFile)
        {
            try
            {
                try
                {
                    foreach (var visual in Application.Current.Windows.OfType<Window>())
                        visual.Hide();
                    IoC.Get<IAudioPlayerToolViewer>()?.AudioPlayer?.Pause();
                }
                catch
                {
                }

                await FileLogOutput.WriteLog(exceptionDetails);
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
                    Application.Current.Invoke(() =>
                    {
                        var exceptionWindow = new ExceptionTermWindow(innerMessage, resuceFolders, logFile, dumpFile);
                        return exceptionWindow.ShowDialog();
                    });
                }
            }
            catch (Exception exceptionHandlerException)
            {
                Debug.WriteLine($"Unhandled exception handler failed : {exceptionHandlerException}");
            }
            finally
            {
                try
                {
                    await Log.WaitForAllLogWriteDone();
                    FileLogOutput.WaitForWriteDone();
                }
                catch (Exception flushException)
                {
                    Debug.WriteLine($"Flush logs after unhandled exception failed : {flushException}");
                }

                Environment.Exit(-1);
            }
        }
#endif

        void ProcessException(object sender, Exception exception, string trigSource)
        {
            if (Interlocked.CompareExchange(ref exceptionHandling, 1, 0) != 0)
                return;

            exception ??= new InvalidOperationException("Unhandled exception did not provide an Exception instance.");

            // AppDomain.UnhandledException is synchronous and the CLR may terminate the
            // process as soon as the callback returns. Write the dump before any await, UI
            // work, or log I/O so those operations cannot prevent the crash artifact.
            var exceptionHandle = Marshal.GetExceptionPointers();
            var dumpFile = string.Empty;
#if !DEBUG
            try
            {
                // Managed exceptions have no native EXCEPTION_POINTERS. DumpFileHelper
                // handles IntPtr.Zero by writing a dump without exception context.
                dumpFile = DumpFileHelper.WriteMiniDump(exceptionHandle) ?? string.Empty;
            }
            catch (Exception dumpException)
            {
                Debug.WriteLine($"Can't write crash dump: {dumpException}");
            }
#endif

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
            sb.AppendLine($"Dump file: {dumpFile}");
            sb.AppendLine("----------------------------");

            try
            {
                // Best-effort minimal log after the dump. A broken log sink must not undo the
                // dump that was already produced.
                FileLogOutput.WriteLog($"trigged by {trigSource}\nDump file: {dumpFile}").GetAwaiter().GetResult();
            }
            catch (Exception logException)
            {
                Debug.WriteLine($"Write minimal crash log failed: {logException}");
            }

#if DEBUG
            throw exception;
#else
            _ = FinishExceptionHandlingAsync(exception, sb.ToString(), innerMessage, dumpFile);
#endif
        }

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception
                ?? new InvalidOperationException(
                    $"Unhandled exception object was {e.ExceptionObject?.GetType().FullName ?? "null"}.");
            ProcessException(sender, exception, "AppDomain.CurrentDomain.UnhandledException");
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
        // Must run first and finish synchronously: code after the first await in OnExit never runs before the process
        // exits, so render backends close their background resources here (Skia lane, OpenGL queue and delayed deletions).
        foreach (var renderManagerImpl in IoC.GetAll<IRenderManagerImpl>())
        {
            try
            {
                // Term() must complete synchronously, so there is nothing to await here; the Task return type just keeps
                // the call site stable if an implementation ever needs to become asynchronous.
                _ = renderManagerImpl.Term();
            }
            catch (Exception ex)
            {
                Log.LogError($"Terminate render manager impl [{renderManagerImpl.Name}] failed: {ex.Message}");
            }
        }

        ipcThread?.Abort();
        await TryStopMcpServerAsync();
        IoC.Get<IAudioManager>().Dispose();
        await IoC.Get<ISchedulerManager>().Term();
        await Log.WaitForAllLogWriteDone();
        try
        {
            await FileLogOutput.WriteLog("\n----------CLOSE FILE LOG OUTPUT----------");
            FileLogOutput.WaitForWriteDone();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Write close file log marker failed : {exception}");
        }
        base.OnExit(sender, e);
    }

    private static bool ShouldAutoStartMcpServer() => ProgramSetting.Default.EnableMcpServerInGUIMode;

    private static async Task TryStartMcpServerAsync()
    {
        if (!ShouldAutoStartMcpServer())
            return;

        try
        {
            await IoC.Get<IMcpServerHost>().StartAsync();
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to start MCP server host: {ex}");
        }
    }

    private static async Task TryStopMcpServerAsync()
    {
        try
        {
            var host = IoC.Get<IMcpServerHost>();
            if (host?.IsRunning ?? false)
                await host.StopAsync();
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to stop MCP server host: {ex}");
        }
    }
}


