using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Iciclecreek.Avalonia.WindowManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Browser.Utils;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;
using OngekiFumenEditor.Avalonia.Browser.Modules.SplashScreen.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen;
using OngekiFumenEditor.Avalonia.Browser.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

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

        serviceCollection.AddSingleton<DefaultBrowserFumenVisualEditorProvider>();
        serviceCollection.AddSingleton<IEditorProvider>(provider =>
            provider.GetRequiredService<DefaultBrowserFumenVisualEditorProvider>());
        serviceCollection.AddSingleton<IFumenVisualEditorProvider>(provider =>
            provider.GetRequiredService<DefaultBrowserFumenVisualEditorProvider>());

        serviceCollection.AddTypeCollectedActivator(BrowserViewTypeCollectedActivator.Default);

        // Core 只提供 Splash 基类；具体平台窗口由组合根显式绑定，不依赖注册顺序。
        serviceCollection.AddSingleton<ISplashScreenWindow>(provider =>
            provider.GetRequiredService<BrowserSplashScreenViewModel>());

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
        serviceCollection.AddSingleton<ILoggerProvider, BrowserFileLoggerProvider>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        logger = ServiceProvider.GetService<ILogger<OngekiFumenEditorBrowserApp>>();
        InitializeProgramVersion();

        var shell = ServiceProvider.GetService<IShell>();

        shell.DockableOpened += AutoSaveLayout;
        shell.DockableClosed += AutoSaveLayout;

        // 关标签页/刷新没有 .NET 侧关闭事件，唯一可用提醒是 JS 的 beforeunload：
        // 把文档脏状态聚合推给 JS，由 JS 在脏时挂接浏览器原生关闭确认框。
        new BeforeUnloadDirtyDocumentGuard(JsApplicationInterop.SetDirtyState).Attach(shell);
    }

    private void InitializeProgramVersion()
    {
        try
        {
            var currentVersion = typeof(OngekiFumenEditorApp).Assembly.GetName().Version ??
                new Version(0, 0, 0, 0);
            var currentVersionString = currentVersion.ToString();
            var setting = ProgramSetting.Default;
            var previousVersionString = setting.__PrevProgramVersionString;

            setting.__PrevProgramVersionString = currentVersionString;
            setting.Save();

            if (string.IsNullOrWhiteSpace(previousVersionString) ||
                !Version.TryParse(previousVersionString, out var previousVersion) ||
                previousVersion.Equals(currentVersion))
                return;

            Dispatcher.UIThread.Post(
                () => _ = ShowProgramUpdateAboutDialogAsync(previousVersion),
                DispatcherPriority.Background);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize browser program version tracking.");
        }
    }

    private async Task ShowProgramUpdateAboutDialogAsync(Version sourceVersion)
    {
        try
        {
            await WaitForSplashScreenHostReadyAsync();
            await ServiceProvider.GetRequiredService<IWindowManager>()
                .ShowDialogAsync(new AboutWindowViewModel(true, sourceVersion));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to show the About dialog after a browser update.");
        }
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
