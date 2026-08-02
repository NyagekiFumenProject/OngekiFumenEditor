using Gekimini.Avalonia;
using Avalonia.Threading;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Kernel.Scheduler;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen;
using OngekiFumenEditor.Avalonia.Utils.ObjectPool;

namespace OngekiFumenEditor.Avalonia.Avalonia;

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

        serviceCollection.AddSingleton<ISchedulable>(provider =>
            provider.GetRequiredService<ObjectPoolManager>());

        serviceCollection.AddTypeCollectedActivator(ViewTypeCollectedActivator.Default);

        serviceCollection.AddTypeCollectedActivator(ToolViewModelTypeCollectedActivator.Default);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        if (!IsGUIMode)
            return;

        Dispatcher.UIThread.Post(
            AttachEditorKeyBindingRouter,
            DispatcherPriority.Background);

        Dispatcher.UIThread.Post(
            () => _ = ShowSplashScreenAfterBootAsync(),
            DispatcherPriority.Background);
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

