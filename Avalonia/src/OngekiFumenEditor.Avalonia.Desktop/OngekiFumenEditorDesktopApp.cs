using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;
using OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Utils.MethodExtensions;
using OngekiFumenEditor.Avalonia.Desktop.Modules.SplashScreen.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XamlMcp.Avalonia;

namespace OngekiFumenEditor.Avalonia.Desktop;

public class OngekiFumenEditorDesktopApp : OngekiFumenEditorApp
{
    private ILogger<OngekiFumenEditorDesktopApp> logger;
    private readonly string[] commandLineArgs;

    public OngekiFumenEditorDesktopApp()
        : this(isGUIMode: true)
    {
    }

    internal OngekiFumenEditorDesktopApp(bool isGUIMode, string[] commandLineArgs = null)
        : base(isGUIMode)
    {
        this.commandLineArgs = commandLineArgs ?? [];
    }

    protected override void RegisterServices(IServiceCollection serviceCollection)
    {
        base.RegisterServices(serviceCollection);

        serviceCollection.AddOngekiFumenEditorAvaloniaDesktop();
        RegisterFumenVisualEditorProvider(serviceCollection);

        // Desktop 视图经 ViewLocator 定位需要平台自己的视图类型收集器。
        serviceCollection.AddTypeCollectedActivator(DesktopViewTypeCollectedActivator.Default);

        // Core 只提供 Splash 基类；具体平台窗口由组合根显式绑定，不依赖注册顺序。
        serviceCollection.AddSingleton<ISplashScreenWindow>(provider =>
            provider.GetRequiredService<DesktopSplashScreenViewModel>());

#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif
        serviceCollection.AddLogging(o =>
        {
            o.SetMinimumLevel(LogLevel.Debug);
            o.AddDebug();
            if (IsGUIMode)
                o.AddConsole();
        });
        serviceCollection.AddSingleton<ILoggerProvider, FileLoggerProvider>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

#if !DEBUG
        Program.InstallDispatcherExceptionHandler();
#endif

        logger = ServiceProvider.GetService<ILogger<OngekiFumenEditorDesktopApp>>();

        if (!IsGUIMode)
        {
            Dispatcher.UIThread.Post(
                () => _ = ExecuteCommandLineAsync(),
                DispatcherPriority.Background);
            return;
        }

#if DEBUG
        this.AttachXamlMcp();
#endif

        ApplyAdminPermissionTitleSuffix();

        Dispatcher.UIThread.Post(
            () => _ = ProcessStartupArgsAsync(),
            DispatcherPriority.Background);
    }

    private void ApplyAdminPermissionTitleSuffix()
    {
        try
        {
            // 对齐 WPF AppBootstrapper：以管理员权限运行时窗口标题加后缀。
            if (!OperatingSystem.IsWindows())
                return;
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (identity is null || !new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
                    System.Security.Principal.WindowsBuiltInRole.Administrator))
                return;

            logger?.LogWarning("Program is within admin permission.");
            var mainWindow = ServiceProvider.GetRequiredService<IPlatformMainWindow>();
            mainWindow.Title += "(以管理员权限运行)";
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to apply the admin permission title suffix.");
        }
    }

    internal static void RegisterFumenVisualEditorProvider(IServiceCollection services)
    {
        services.AddSingleton<DefaultDesktopFumenVisualEditorProvider>();
        services.AddSingleton<IEditorProvider>(provider =>
            provider.GetRequiredService<DefaultDesktopFumenVisualEditorProvider>());
        services.AddSingleton<IFumenVisualEditorProvider>(provider =>
            provider.GetRequiredService<DefaultDesktopFumenVisualEditorProvider>());
    }

    private async Task ProcessStartupArgsAsync()
    {
        try
        {
            await ServiceProvider.GetRequiredService<IProgramArgProcessManager>()
                .ProcessArgs(Program.StartupArgs);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to process the startup arguments.");
        }
    }

    private async Task ExecuteCommandLineAsync()
    {
        var exitCode = 1;
        try
        {
            exitCode = await ServiceProvider.GetRequiredService<ICommandExecutor>()
                .ExecuteAsync(commandLineArgs);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Failed to execute the command line.");
            await Console.Error.WriteLineAsync(exception.Message);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown(exitCode);
        else
            Environment.Exit(exitCode);
    }

    protected override void DoExit(int exitCode = 0)
    {
        /*
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;
        desktop.Shutdown(exitCode);
        */
        logger.LogInformationEx("bye.");
        Environment.Exit(exitCode);
    }
}
