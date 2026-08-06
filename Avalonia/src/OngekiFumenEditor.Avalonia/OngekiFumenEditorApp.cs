using Gekimini.Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Platforms.Services.MainWindow;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        InitializeMainWindowTitleAndIcon();

        Dispatcher.UIThread.Post(
            AttachEditorKeyBindingRouter,
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
            ServiceProvider.GetRequiredService<ILogger<OngekiFumenEditorApp>>()
                .LogError(exception, "Failed to initialize the main window title and icon.");
        }
    }

    private void AttachEditorKeyBindingRouter()
    {
        try
        {
            ServiceProvider.GetRequiredService<IEditorKeyBindingRouter>().Attach(TopLevel);
        }
        catch (Exception exception)
        {
            ServiceProvider.GetRequiredService<ILogger<OngekiFumenEditorApp>>()
                .LogError(exception, "Failed to attach the editor key binding router.");
        }
    }

    private void AttachEditorDocumentManagerBridge()
    {
        try
        {
            var shell = ServiceProvider.GetRequiredService<IShell>();
            var documentManager = ServiceProvider.GetRequiredService<IEditorDocumentManager>();

            // WPF 版由 FumenVisualEditorViewModel 的 Caliburn 生命周期钩子自行调用
            // NotifyCreate/NotifyActivate/NotifyDestory；Gekimini 没有等价钩子，
            // 这里改为订阅 IShell 的 Dock 事件转发，对齐原项目语义。
            shell.DockableOpened += (_, dockable) =>
            {
                if (dockable is FumenVisualEditorViewModel editor)
                    documentManager.NotifyCreate(editor);
            };
            shell.ActiveDocumentChanged += (_, document) =>
            {
                var previous = documentManager.CurrentActivatedEditor;
                if (ReferenceEquals(previous, document))
                    return;
                // 对齐 WPF OnDeactivateAsync：切走编辑器时暂停音频。
                previous?.AudioPlayer?.Pause();
                if (document is FumenVisualEditorViewModel editor)
                    documentManager.NotifyActivate(editor);
                else if (previous is not null)
                    documentManager.NotifyDeactivate(previous);
            };
            shell.DockableClosed += (_, dockable) =>
            {
                if (dockable is FumenVisualEditorViewModel editor)
                    documentManager.NotifyDestory(editor);
            };

            // 桥接前已打开的文档（如布局恢复）补登记。
            foreach (var editor in shell.Documents.OfType<FumenVisualEditorViewModel>())
                documentManager.NotifyCreate(editor);
            if (shell.ActiveDocument is FumenVisualEditorViewModel activeEditor)
                documentManager.NotifyActivate(activeEditor);
        }
        catch (Exception exception)
        {
            ServiceProvider.GetRequiredService<ILogger<OngekiFumenEditorApp>>()
                .LogError(exception, "Failed to attach the editor document manager bridge.");
        }
    }

    private async Task ShowSplashScreenAfterBootAsync()
    {
        if (ProgramSetting.Default.DisableShowSplashScreenAfterBoot)
            return;

        var shell = ServiceProvider.GetRequiredService<IShell>();
        if (shell.Documents.Any())
            return;

        try
        {
            var splashScreen = ServiceProvider.GetRequiredService<ISplashScreenWindow>();
            await ServiceProvider.GetRequiredService<IWindowManager>()
                .ShowWindowAsync(splashScreen.WindowViewModel);
        }
        catch (Exception exception)
        {
            ServiceProvider.GetRequiredService<ILogger<OngekiFumenEditorApp>>()
                .LogError(exception, "Failed to show the splash screen after startup.");
        }
    }
}

