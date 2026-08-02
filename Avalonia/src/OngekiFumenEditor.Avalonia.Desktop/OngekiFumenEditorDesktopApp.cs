using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Gekimini.Avalonia;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Avalonia;
using OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;
using OngekiFumenEditor.Avalonia.Kernel.ArgProcesser;
using Gekimini.Avalonia.Utils;
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

#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif
        serviceCollection.AddLogging(o =>
        {
            o.SetMinimumLevel(LogLevel.Debug);
            o.AddProvider(new FileLoggerProvider());
            o.AddDebug();
            if (IsGUIMode)
                o.AddConsole();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

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

        Dispatcher.UIThread.Post(
            () => _ = ProcessStartupArgsAsync(),
            DispatcherPriority.Background);
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
