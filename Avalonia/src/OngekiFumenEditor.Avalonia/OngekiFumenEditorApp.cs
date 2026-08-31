using Gekimini.Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.DebugInfomation;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen;

namespace OngekiFumenEditor.Avalonia;

public abstract class OngekiFumenEditorApp : App
{
    protected OngekiFumenEditorApp(bool isGUIMode = true)
        : base(isGUIMode)
    {
        // Keep a stable managed ID even when the host does not expose a
        // conventional Program.Main entry point (for example headless tests).
        ThreadingDiagnosticsRuntime.CaptureMainThread();
    }

    protected override void RegisterServices(IServiceCollection serviceCollection)
    {
        base.RegisterServices(serviceCollection);

        serviceCollection.AddOngekiFumenEditorAvalonia();

        serviceCollection.AddTypeCollectedActivator(ViewTypeCollectedActivator.Default);

        serviceCollection.AddTypeCollectedActivator(ToolViewModelTypeCollectedActivator.Default);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        global::OngekiFumenEditor.Avalonia.Utils.Log.Initialize(
            ServiceProvider.GetRequiredService<global::OngekiFumenEditor.Avalonia.Utils.Log>());

        if (!IsGUIMode)
            return;

        // 对齐 WPF AppBootstrapper：启动调度循环（性能统计/自动保存等任务依赖它）。
        _ = ServiceProvider.GetRequiredService<ISchedulerManager>().Init();

        InitializeMainWindowTitleAndIcon();

        Dispatcher.UIThread.Post(
            () => _ = AttachEditorKeyBindingRouterAsync(),
            DispatcherPriority.Background);

        Dispatcher.UIThread.Post(
            AttachEditorDocumentManagerBridge,
            DispatcherPriority.Background);

        Dispatcher.UIThread.Post(
            () => _ = ShowSplashScreenAfterBootAsync(),
            DispatcherPriority.Background);
    }

    private void InitializeMainWindowTitleAndIcon()
    {
        try
        {
            var mainWindow = ServiceProvider.GetRequiredService<IPlatformMainWindow>();
            // 对齐 WPF AppBootstrapper：启动时写入默认窗口标题和 logo 图标。
            mainWindow.Icon = new WindowIcon(AssetLoader.Open(
                new Uri("avares://OngekiFumenEditor.Avalonia/Resources/Icons/logo32.ico")));
            mainWindow.Title = "Ongeki Fumen Editor";
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to initialize the main window title and icon.", exception);
        }
    }

    private async Task AttachEditorKeyBindingRouterAsync()
    {
        try
        {
            await ServiceProvider.GetRequiredService<IKeyBindingManager>().Initialize();
            ServiceProvider.GetRequiredService<IEditorKeyBindingRouter>().Attach(TopLevel);
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to attach the editor key binding router.", exception);
        }
    }

    private void AttachEditorDocumentManagerBridge()
    {
        try
        {
            var shell = ServiceProvider.GetRequiredService<IShell>();
            var documentManager = ServiceProvider.GetRequiredService<IEditorDocumentManager>();
            var schedulerManager = ServiceProvider.GetRequiredService<ISchedulerManager>();

            // WPF 版由 FumenVisualEditorViewModel 的 Caliburn 生命周期钩子自行调用
            // NotifyCreate/NotifyActivate/NotifyDestory；Gekimini 没有等价钩子，
            // 这里改为订阅 IShell 的 Dock 事件转发，对齐原项目语义。
            shell.DockableOpened += (_, dockable) =>
            {
                Log.LogInfo($"Shell dockable opened: {dockable.GetType().FullName}.");
                if (dockable is FumenVisualEditorViewModel editor)
                    documentManager.NotifyCreate(editor);
            };
            shell.ActiveDocumentChanged += (_, document) =>
            {
                var previous = documentManager.CurrentActivatedEditor;
                if (ReferenceEquals(previous, document))
                    return;
                Log.LogInfo($"Active document changed from {previous?.GetType().FullName ?? "(none)"} to {document?.GetType().FullName ?? "(none)"}.");
                // 对齐 WPF OnDeactivateAsync：切走编辑器时暂停音频、摘除调度任务。
                if (previous is not null)
                {
                    previous.AudioPlayer?.Pause();
                    if (schedulerManager.Schedulers.Contains(previous))
                        _ = schedulerManager.RemoveScheduler(previous);
                }
                // 对齐 WPF OnActivateAsync：激活编辑器时注册调度任务（性能统计等）。
                if (document is FumenVisualEditorViewModel editor)
                {
                    _ = schedulerManager.AddScheduler(editor);
                    documentManager.NotifyActivate(editor);
                }
                else if (previous is not null)
                    documentManager.NotifyDeactivate(previous);
            };
            shell.DockableClosed += (_, dockable) =>
            {
                Log.LogInfo($"Shell dockable closed: {dockable.GetType().FullName}.");
                if (dockable is FumenVisualEditorViewModel editor)
                {
                    if (schedulerManager.Schedulers.Contains(editor))
                        _ = schedulerManager.RemoveScheduler(editor);
                    documentManager.NotifyDestory(editor);
                }
            };

            // 桥接前已打开的文档（如布局恢复）补登记。
            foreach (var editor in shell.Documents.OfType<FumenVisualEditorViewModel>())
                documentManager.NotifyCreate(editor);
            if (shell.ActiveDocument is FumenVisualEditorViewModel activeEditor)
            {
                _ = schedulerManager.AddScheduler(activeEditor);
                documentManager.NotifyActivate(activeEditor);
            }
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to attach the editor document manager bridge.", exception);
        }
    }

    protected override async Task PrepareExit()
    {
        await base.PrepareExit();

        // 对齐 WPF AppBootstrapper.OnExit：退出前终止调度循环。
        try
        {
            var schedulerManager = ServiceProvider.GetService<ISchedulerManager>();
            if (schedulerManager is not null)
                await schedulerManager.Term();
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to terminate the scheduler manager.", exception);
        }
    }

    /// <summary>
    /// Gives a single-view platform a chance to finish laying out the window host
    /// before a managed window is added to it.
    /// </summary>
    protected virtual Task WaitForSplashScreenHostReadyAsync()
    {
        return Task.CompletedTask;
    }

    private async Task ShowSplashScreenAfterBootAsync()
    {
        if (ProgramSetting.Default.DisableShowSplashScreenAfterBoot)
            return;

        try
        {
            await WaitForSplashScreenHostReadyAsync();

            var shell = ServiceProvider.GetRequiredService<IShell>();
            if (shell.Documents.Any())
                return;

            var splashScreen = ServiceProvider.GetRequiredService<ISplashScreenWindow>();
            await ServiceProvider.GetRequiredService<IWindowManager>()
                .ShowWindowAsync(splashScreen.WindowViewModel);
        }
        catch (Exception exception)
        {
            Log.LogError("Failed to show the splash screen after boot.", exception);
        }
    }
}

