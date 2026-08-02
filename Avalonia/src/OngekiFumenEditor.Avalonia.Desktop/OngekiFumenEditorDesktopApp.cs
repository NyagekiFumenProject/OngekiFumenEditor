using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Gekimini.Avalonia;
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
            o.AddConsole();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

#if DEBUG
        this.AttachXamlMcp();
#endif

        logger = ServiceProvider.GetService<ILogger<OngekiFumenEditorDesktopApp>>();

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
