using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Gekimini.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Desktop.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop;

internal class Program
{
    private static bool exceptionHandling;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
#if !DEBUG
        AppDomain.CurrentDomain.UnhandledException += async (sender, e) =>
        {
            ProcessException(sender, e.ExceptionObject as Exception, "AppDomain.CurrentDomain.UnhandledException");
        };
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            ProcessException(sender, e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };
#endif
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void ProcessException(object sender, Exception exception, string trigSource)
    {
        if (exceptionHandling)
            return;
        exceptionHandling = true;

        var app = Application.Current as App;
        var logger = app?.ServiceProvider?.GetService<ILogger<Program>>();
        logger?.LogInformationEx($"trigged by {trigSource}");

        try
        {
            if (app != null)
            {
                var windows = (app.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                    ?.Windows;
                if (windows != null)
                    foreach (var window in windows)
                        window.Hide();
            }
        }
        catch
        {
            // ignored
        }

        var (message, callstack) = TravalInnerExceptionMessage(exception) ?? ("<NO EXCEPTION>", string.Empty);
        var content = $"程序遇到致命错误，即将关闭，相关日志已保存。\n错误原因:{message}\nCallStack:{callstack}";

        logger.LogErrorEx(content);
        NativeMessageBox.Show(content);

        Environment.Exit(-1);

        exceptionHandling = false;

        (string message, string callstack)? TravalInnerExceptionMessage(Exception e)
        {
            return e is null ? null : TravalInnerExceptionMessage(e.InnerException) ?? (e.Message, e.StackTrace);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<ExampleDesktopApp>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}