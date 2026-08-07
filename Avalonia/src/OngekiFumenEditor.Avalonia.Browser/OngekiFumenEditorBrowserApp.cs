using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Utils;
using Iciclecreek.Avalonia.WindowManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Browser.Utils;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;

namespace OngekiFumenEditor.Avalonia.Browser;

public class OngekiFumenEditorBrowserApp : OngekiFumenEditorApp
{
    // Keep the host above DefaultWindowManager's minimum managed-window size.
    private const double MinimumWindowHostLength = 50;
    private ILogger<OngekiFumenEditorBrowserApp> logger;

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
        serviceCollection.AddSingleton<ILoggerProvider, TemporaryFileLoggerProvider>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        logger = ServiceProvider.GetService<ILogger<OngekiFumenEditorBrowserApp>>();
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

    protected override async Task WaitForSplashScreenHostReadyAsync()
    {
        if (ApplicationLifetime is not ISingleViewApplicationLifetime singleView ||
            singleView.MainView is not { } mainView)
            return;

        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void CheckHostLayout(object sender, EventArgs args)
        {
            var windowPanel = mainView.FindDescendantOfType<WindowsPanel>(true);
            if (windowPanel is null ||
                windowPanel.Bounds.Width < MinimumWindowHostLength ||
                windowPanel.Bounds.Height < MinimumWindowHostLength)
                return;

            mainView.LayoutUpdated -= CheckHostLayout;
            ready.TrySetResult(true);
        }

        mainView.LayoutUpdated += CheckHostLayout;
        CheckHostLayout(mainView, EventArgs.Empty);

        await ready.Task;
    }
}
