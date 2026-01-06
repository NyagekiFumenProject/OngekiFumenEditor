using System;
using Gekimini.Avalonia;
using OngekiFumenEditor.Avalonia.Desktop.Utils.Logging;
using Gekimini.Avalonia.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Desktop;

public class ExampleDesktopApp : ExampleApp
{
    private ILogger<ExampleDesktopApp> logger;

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

        logger = ServiceProvider.GetService<ILogger<ExampleDesktopApp>>();
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