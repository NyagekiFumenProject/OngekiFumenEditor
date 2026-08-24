using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Utils;
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

    public DesktopSplashScreenViewModel(
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
        this.fastOpenService = fastOpenService;
    }

    [RelayCommand]
    private Task FastOpenAsync()
    {
        Log.LogInfo("FastOpenAsync triggered.");
        return fastOpenService.OpenAsync();
    }

}
