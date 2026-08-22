using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen.ViewModels;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.SplashScreen.ViewModels;

[RegisterSingleton]
public sealed partial class DesktopSplashScreenViewModel : SplashScreenViewModelBase
{
    public DesktopSplashScreenViewModel(
        ILanguageManager languageManager,
        Gekimini.Avalonia.Platforms.Services.Settings.ISettingManager settingManager,
        IEditorRecentFilesManager recentFilesManager,
        IRecentRecordValidityCoordinator recentRecordValidityCoordinator,
        IFumenVisualEditorProvider editorProvider,
        Gekimini.Avalonia.Framework.Commands.ICommandService commandService,
        IShell shell)
        : base(
            languageManager,
            settingManager,
            recentFilesManager,
            recentRecordValidityCoordinator,
            editorProvider,
            commandService,
            shell)
    {
    }
}
