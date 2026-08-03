using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Gekimini.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Desktop.Utils;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Desktop;

internal class Program
{
    private static bool exceptionHandling;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        StartupArgs = args ?? [];

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
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    internal static string[] StartupArgs { get; private set; } = [];

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
        => BuildAvaloniaApp(static () => new OngekiFumenEditorDesktopApp());

    internal static AppBuilder BuildAvaloniaApp(Func<OngekiFumenEditorDesktopApp> appFactory)
    {
        return AppBuilder.Configure(appFactory)
            .UsePlatformDetect()
            .AfterPlatformServicesSetup(_ => InstallResourceOverrideAssetLoader(
                Path.Combine(AppContext.BaseDirectory, "Resources")))
            .WithInterFont()
            .LogToTrace();
    }

    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicProperties |
        DynamicallyAccessedMemberTypes.NonPublicFields,
        typeof(AvaloniaLocator))]
    internal static void InstallResourceOverrideAssetLoader(string overrideRootPath)
    {
        const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        var locatorType = typeof(AvaloniaLocator);
        var currentMutable = locatorType.GetProperty("CurrentMutable", staticFlags)?.GetValue(null)
            ?? throw new InvalidOperationException("Avalonia mutable service locator is unavailable.");
        var registry = locatorType.GetField("_registry", instanceFlags)?.GetValue(currentMutable) as IDictionary
            ?? throw new InvalidOperationException("Avalonia service registry is unavailable.");
        var assetLoader = new ResourceOverrideAssetLoader(
            new StandardAssetLoader(typeof(OngekiFumenEditorDesktopApp).Assembly),
            overrideRootPath);

        registry[typeof(IAssetLoader)] = (Func<object>)(() => assetLoader);
    }
}
