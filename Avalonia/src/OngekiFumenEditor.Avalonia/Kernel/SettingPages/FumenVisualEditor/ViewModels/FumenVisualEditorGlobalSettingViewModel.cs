using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.Platforms.Services.Window;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class FumenVisualEditorGlobalSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly IDialogManager dialogManager;
    private readonly IEditorRecentFilesManager recentFilesManager;
    private readonly ILogger<FumenVisualEditorGlobalSettingViewModel> logger;

    public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

    public string SettingsPageName => Lang.TabEditor;

    public string SettingsPagePath => Lang.TabDocument;

    public FumenVisualEditorGlobalSettingViewModel(
        ILogger<FumenVisualEditorGlobalSettingViewModel> logger)
        : this(logger, null, null)
    {
    }

    internal FumenVisualEditorGlobalSettingViewModel(
        ILogger<FumenVisualEditorGlobalSettingViewModel> logger,
        IDialogManager dialogManager,
        IEditorRecentFilesManager recentFilesManager)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dialogManager = dialogManager;
        this.recentFilesManager = recentFilesManager;
        EditorGlobalSetting.Default.PropertyChanged += (_, e) => Log.LogDebug($"editor global setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        EditorGlobalSetting.Default.Save();
    }

    [RelayCommand]
    private Task SelectBackgroundColorAsync()
    {
        logger.LogInformation("SelectBackgroundColorAsync triggered.");
        return ShowColorPickerAsync(
            () => System.Drawing.Color.FromArgb(Setting.PlayFieldBackgroundColor).ToMediaColor(),
            color => Setting.PlayFieldBackgroundColor = color.ToDrawingColor().ToArgb(),
            Lang.NamedColorChangeTitle.Format(Lang.BackgroundColor));
    }

    [RelayCommand]
    private Task SelectForegroundColorAsync()
    {
        logger.LogInformation("SelectForegroundColorAsync triggered.");
        return ShowColorPickerAsync(
            () => System.Drawing.Color.FromArgb(Setting.PlayFieldForegroundColor).ToMediaColor(),
            color => Setting.PlayFieldForegroundColor = color.ToDrawingColor().ToArgb(),
            Lang.NamedColorChangeTitle.Format(Lang.PlayFieldForegroundColor));
    }

    [RelayCommand]
    private async Task ClearRecentFilesAsync()
    {
        logger.LogInformation("ClearRecentFilesAsync triggered.");
        var dialog = dialogManager ?? IoC.Get<IDialogManager>();
        if (!await dialog.ShowComfirmDialog(Lang.CleanRecentFilesRecordsConfirm, Lang.Warning))
            return;

        (recentFilesManager ?? IoC.Get<IEditorRecentFilesManager>()).ClearAllRecordsAndDatas();
    }

    private Task ShowColorPickerAsync(
        Func<global::Avalonia.Media.Color> getter,
        Action<global::Avalonia.Media.Color> setter,
        string title)
    {
        return IoC.Get<IWindowManager>().ShowWindowAsync(new CommonColorPickerViewModel(getter, setter, title));
    }
}

