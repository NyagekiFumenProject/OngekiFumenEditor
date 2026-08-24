using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.Utils.DeadHandler;
using OngekiFumenEditor.Avalonia.Desktop.Utils;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XamlMcp.Avalonia;

namespace OngekiFumenEditor.Avalonia.Desktop;

public class OngekiFumenEditorDesktopApp : OngekiFumenEditorApp
{
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

        // Desktop 视图经 ViewLocator 定位需要平台自己的视图类型收集器。
        serviceCollection.AddTypeCollectedActivator(DesktopViewTypeCollectedActivator.Default);
#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif
        serviceCollection.AddLogging(o =>
        {
            o.SetMinimumLevel(LogLevel.Debug);
            o.AddDebug();
            // 仅命令行模式保留 MEL 终端直显；GUI 的控制台由 ConsoleWindowHelper
            // 挂载的着色 DesktopConsoleLogOutput 承担，MEL 流量经 MELTransportLoggerProvider
            // 进入门面广播(文件+控制台)。
            if (!IsGUIMode)
                o.AddConsole();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

#if !DEBUG
        Program.InstallDispatcherExceptionHandler();
#endif

        // 原生顶层过滤器：托管管道覆盖不到的原生崩溃（P/Invoke 访问冲突等）也写出 minidump。
        // 需在 DI 容器构建完成后执行，Init 内部要读取 ProgramSetting。
        if (OperatingSystem.IsWindows())
        {
            try
            {
                DumpFileHelper.Init();
            }
            catch (Exception exception)
            {
                Log.LogError("Failed to install native unhandled exception filter.", exception);
            }
        }

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

        if (OperatingSystem.IsWindows())
        {
#if DEBUG
            var showConsole = true;
#else
            var showConsole = ProgramSetting.Default.ShowConsoleWindowInGUIMode;
#endif
            ConsoleWindowHelper.SetConsoleWindowVisible(showConsole);

            ProgramSetting.Default.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProgramSetting.ShowConsoleWindowInGUIMode))
                    ConsoleWindowHelper.SetConsoleWindowVisible(ProgramSetting.Default.ShowConsoleWindowInGUIMode);
            };
        }

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

            Log.LogWarn("Program is within admin permission.");
            var mainWindow = ServiceProvider.GetRequiredService<IPlatformMainWindow>();
            mainWindow.Title += "(以管理员权限运行)";
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to apply the admin permission title suffix.", exception);
        }
    }

    private static void ApplyConsoleVisibility(bool show)
    {
        // 对齐 WPF AppBootstrapper：OS 控制台承载着色日志输出；DEBUG 恒显示，
        // Release 由 ShowConsoleWindowInGUIMode 决定，设置页可实时切换。
        // ConsoleWindowHelper 内部已联动 Log 输出的挂载与摘除。
        ConsoleWindowHelper.SetConsoleWindowVisible(show);
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
            Log.LogError("Failed to process the startup arguments.", exception);
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
            Log.LogError("Failed to execute the command line.", exception);
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
        Log.LogInfo("bye.");
        Environment.Exit(exitCode);
    }
}
