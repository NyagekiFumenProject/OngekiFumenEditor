using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Settings;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class LogsSettingViewModel : ViewModelBase, ISettingsEditor
{
    public LogSetting Setting => LogSetting.Default;

    public string SettingsPageName => Lang.TabLogger;

    public string SettingsPagePath => Lang.TabEnviorment;

    public LogsSettingViewModel()
    {
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"logs setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }

    [RelayCommand]
    private async Task SelectLogsFolderAsync()
    {
        using var folder = await FileDialogHelper.OpenDirectoryAsync(Lang.LoggerFolder);
        if (string.IsNullOrWhiteSpace(folder?.LocalPath))
            return;

        Setting.LogFileDirPath = folder.LocalPath;
        ApplyChanges();
    }
}

