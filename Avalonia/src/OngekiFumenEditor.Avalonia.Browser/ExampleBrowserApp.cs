using System;
using Gekimini.Avalonia;
using OngekiFumenEditor.Avalonia.Browser.Utils;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OngekiFumenEditor.Avalonia.Browser;

public class ExampleBrowserApp : ExampleApp
{
    private ILogger<ExampleBrowserApp> logger;

    protected override void RegisterServices(IServiceCollection serviceCollection)
    {
        base.RegisterServices(serviceCollection);

#if LLVM_BUILD
        serviceCollection.AddOngekiFumenEditorAvaloniaBrowserLLVM();
#else
        serviceCollection.AddOngekiFumenEditorAvaloniaBrowser();
#endif

#if DEBUG
        if (DesignModeHelper.IsDesignMode)
            return;
#endif

        serviceCollection.AddLogging(o =>
        {
            o.SetMinimumLevel(LogLevel.Debug);
            o.AddProvider(new ConsoleLoggerProvider());
            o.AddDebug();
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        logger = ServiceProvider.GetService<ILogger<ExampleBrowserApp>>();
        var shell = ServiceProvider.GetService<IShell>();

        shell.DockableOpened += AutoSaveLayout;
        shell.DockableClosed += AutoSaveLayout;
    }

    private void AutoSaveLayout(object sender, IDockableViewModel e)
    {
        if (e is not IToolViewModel)
            return;
        ServiceProvider.GetService<IShell>().SaveLayout();
    }

    protected override void DoExit(int exitCode = 0)
    {
        logger.LogInformationEx($"bye. exitCode={exitCode}");
        JsApplicationInterop.Exit();
    }
}