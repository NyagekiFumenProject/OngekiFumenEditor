using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public EditorGlobalSetting Setting => EditorGlobalSetting.Default;

    public string SettingsPageName => Lang.TabEditor;

    public string SettingsPagePath => Lang.TabDocument;

    public FumenVisualEditorGlobalSettingViewModel() : this(null, null)
    {
    }

    internal FumenVisualEditorGlobalSettingViewModel(
        IDialogManager dialogManager,
        IEditorRecentFilesManager recentFilesManager)
    {
        this.dialogManager = dialogManager;
        this.recentFilesManager = recentFilesManager;
        EditorGlobalSetting.Default.PropertyChanged += (_, e) => Log.LogDebug($"editor global setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        EditorGlobalSetting.Default.Save();
    }

    [RelayCommand]
    private Task SelectBackgroundColorAsync() => ShowColorPickerAsync(
        () => System.Drawing.Color.FromArgb(Setting.PlayFieldBackgroundColor).ToMediaColor(),
        color => Setting.PlayFieldBackgroundColor = color.ToDrawingColor().ToArgb(),
        Lang.NamedColorChangeTitle.Format(Lang.BackgroundColor));

    [RelayCommand]
    private Task SelectForegroundColorAsync() => ShowColorPickerAsync(
        () => System.Drawing.Color.FromArgb(Setting.PlayFieldForegroundColor).ToMediaColor(),
        color => Setting.PlayFieldForegroundColor = color.ToDrawingColor().ToArgb(),
        Lang.NamedColorChangeTitle.Format(Lang.PlayFieldForegroundColor));

    [RelayCommand]
    private async Task ClearRecentFilesAsync()
    {
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

