using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Gekimini.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.UI.Dialogs;
using OngekiFumenEditor.Avalonia.Desktop.Utils;
using OngekiFumenEditor.Avalonia.Utils.DeadHandler;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop;

internal class Program
{
    private static readonly HashSet<IntPtr> recordedExceptionHandles = [];
    private static readonly object exceptionHandleLock = new();
    private static int exceptionHandling;
    private static int dispatcherExceptionHandlerInstalled;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        StartupArgs = args ?? [];

#if !DEBUG
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            ProcessException(sender, e.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            ProcessException(sender, e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
#endif
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    internal static string[] StartupArgs { get; private set; } = [];

    internal static void InstallDispatcherExceptionHandler()
    {
        if (Interlocked.Exchange(ref dispatcherExceptionHandlerInstalled, 1) != 0)
            return;

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    private static void ProcessException(object sender, Exception exception, string trigSource)
    {
        if (Interlocked.Exchange(ref exceptionHandling, 1) != 0)
            return;

        var app = Application.Current as App;
        if (app is null)
        {
            ShowFallbackAndExit(exception, trigSource);
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            _ = ProcessExceptionOnUiThreadAsync(sender, exception, trigSource);
            return;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Dispatcher.UIThread.Post(async () =>
            {
                started.TrySetResult();
                try
                {
                    await ProcessExceptionOnUiThreadAsync(sender, exception, trigSource);
                }
                finally
                {
                    completed.TrySetResult();
                }
            });

            if (!started.Task.Wait(TimeSpan.FromSeconds(5)))
            {
                ShowFallbackAndExit(exception, trigSource);
                return;
            }

            completed.Task.GetAwaiter().GetResult();
        }
        catch (Exception handlerException)
        {
            ShowFallbackAndExit(new AggregateException(exception, handlerException), trigSource);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ProcessException(sender, e.Exception, "Dispatcher.UIThread.UnhandledException");
    }

    private static async Task ProcessExceptionOnUiThreadAsync(object sender, Exception exception, string trigSource)
    {
        try
        {
            Dispatcher.UIThread.VerifyAccess();

            var app = Application.Current as App;
            Log.LogInfo($"triggered by {trigSource}");

            HideApplicationWindows(app);

            var (innerMessage, report) = BuildExceptionReport(sender, exception, trigSource);
            Log.LogError(report, exception);
            await TryWriteLogAsync(report);

            var dumpFile = TryWriteMiniDump();
            await TryWriteLogAsync("FumenRescue.Rescue() Begin\n");
            var rescueFolders = await TryRescueFumenAsync();
            await TryWriteLogAsync("FumenRescue.Rescue() End\n");

            await TryWaitForLogWritesAsync();
            var logFile = TryGetCurrentLogFile();

            await ShowExceptionWindowAsync(innerMessage, rescueFolders, logFile, dumpFile);
        }
        catch (Exception handlerException)
        {
            ShowFallback(BuildFallbackMessage(new AggregateException(exception, handlerException), trigSource));
        }
        finally
        {
            Environment.Exit(-1);
        }
    }

    private static void HideApplicationWindows(App app)
    {
        try
        {
            var windows = (app?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;
            if (windows is null)
                return;

            foreach (var window in windows.ToArray())
                window.Hide();
        }
        catch
        {
            // ignored
        }
    }

    private static (string InnerMessage, string Report) BuildExceptionReport(
        object sender,
        Exception exception,
        string trigSource)
    {
        var innerMessage = exception?.Message ?? "<NO EXCEPTION>";
        var builder = new StringBuilder();
        builder.AppendLine("----------Exception Catcher----------");
        builder.AppendLine($"Triggered by {trigSource} from object: {sender} ({sender?.GetType().FullName})");

        var level = 0;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            innerMessage = current.Message;
            var indent = new string('\t', level * 2);
            builder.AppendLine();
            builder.AppendLine($"{indent}Exception lv.{level}: {current.Message}");
            builder.AppendLine($"{indent}Stack: {current.StackTrace}");
            level++;
        }

        builder.AppendLine("----------------------------");
        return (innerMessage, builder.ToString());
    }

    private static async Task TryWriteLogAsync(string content)
    {
        try
        {
            await FileLogOutput.WriteLog(content);
        }
        catch
        {
            // ignored
        }
    }

    private static async Task TryWaitForLogWritesAsync()
    {
        try
        {
            await Log.WaitForAllLogWriteDone();
        }
        catch
        {
            // ignored
        }
    }

    private static string TryGetCurrentLogFile()
    {
        try
        {
            return FileLogOutput.GetCurrentLogFile();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryWriteMiniDump()
    {
        if (!OperatingSystem.IsWindows())
            return string.Empty;

        try
        {
            var exceptionHandle = Marshal.GetExceptionPointers();
            if (exceptionHandle == IntPtr.Zero)
                return string.Empty;

            lock (exceptionHandleLock)
            {
                if (!recordedExceptionHandles.Add(exceptionHandle))
                    return string.Empty;
            }

            return DumpFileHelper.WriteMiniDump(exceptionHandle);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string[]> TryRescueFumenAsync()
    {
        try
        {
            return await FumenRescue.Rescue();
        }
        catch
        {
            return [];
        }
    }

    private static Task ShowExceptionWindowAsync(
        string exceptionMessage,
        string[] rescueFolderPaths,
        string logFile,
        string dumpFile)
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exceptionWindow = new ExceptionTermWindow(
            exceptionMessage,
            rescueFolderPaths,
            logFile,
            dumpFile);
        exceptionWindow.Closed += (_, _) => closed.TrySetResult();
        exceptionWindow.Show();
        return closed.Task;
    }

    private static void ShowFallbackAndExit(Exception exception, string trigSource)
    {
        ShowFallback(BuildFallbackMessage(exception, trigSource));
        Environment.Exit(-1);
    }

    private static string BuildFallbackMessage(Exception exception, string trigSource)
    {
        var message = exception?.GetBaseException().Message ?? "<NO EXCEPTION>";
        return $"程序遇到致命错误，即将关闭，相关日志已保存。\n触发来源:{trigSource}\n错误原因:{message}\nCallStack:{exception?.StackTrace}";
    }

    private static void ShowFallback(string content)
    {
        try
        {
            NativeMessageBox.Show(content);
        }
        catch
        {
            // ignored
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => BuildAvaloniaApp(static () => new OngekiFumenEditorDesktopApp());

    internal static AppBuilder BuildAvaloniaApp(Func<OngekiFumenEditorDesktopApp> appFactory)
    {
        return AppBuilder.Configure(appFactory)
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
