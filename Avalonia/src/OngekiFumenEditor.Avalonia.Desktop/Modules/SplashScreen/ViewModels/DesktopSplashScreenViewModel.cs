using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.SplashScreen.ViewModels;

[RegisterSingleton<ISplashScreenWindow>]
public sealed partial class DesktopSplashScreenViewModel : SplashScreenViewModelBase
{
    private readonly DesktopFastOpenService fastOpenService;
    private readonly ILogger<DesktopSplashScreenViewModel> logger;

    public DesktopSplashScreenViewModel(
        ILogger<DesktopSplashScreenViewModel> logger,
        ILanguageManager languageManager,
        Gekimini.Avalonia.Platforms.Services.Settings.ISettingManager settingManager,
        IEditorRecentFilesManager recentFilesManager,
        IRecentRecordValidityCoordinator recentRecordValidityCoordinator,
        IFumenVisualEditorProvider editorProvider,
        Gekimini.Avalonia.Framework.Commands.ICommandService commandService,
        IShell shell,
        DesktopFastOpenService fastOpenService)
        : base(
            languageManager,
            settingManager,
            recentFilesManager,
            recentRecordValidityCoordinator,
            editorProvider,
            commandService,
            shell)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.fastOpenService = fastOpenService;
    }

    [RelayCommand]
    private Task FastOpenAsync()
    {
        logger.LogInformation("FastOpenAsync triggered.");
        return fastOpenService.OpenAsync();
    }

}
