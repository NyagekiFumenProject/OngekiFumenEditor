using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.ViewModels;

public partial class ProgramSettingViewModel : ViewModelBase
{
    public ProgramSetting Setting => ProgramSetting.Default;

    public ProgramSettingViewModel()
    {
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"program setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }

    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        if (!await IoC.Get<IDialogManager>().ShowComfirmDialog(Lang.ResetAllSettingComfirm, Lang.Warning))
            return;

        var settingList = new ISettingModel[]
        {
            AudioPlayerToolViewerSetting.Default,
            AudioSetting.Default,
            EditorGlobalSetting.Default,
            LogSetting.Default,
            ProgramSetting.Default,
            KeyBindingSetting.Default,
            DefaultWaveformSettings.Default,
        };

        foreach (var setting in settingList)
        {
            setting.Reset();
            setting.Save();
        }

        await IoC.Get<IDialogManager>().ShowMessageDialog(Lang.ResetCompleted);
    }

    [RelayCommand]
    private async Task SelectDumpFolderAsync()
    {
        using var folder = await FileDialogHelper.OpenDirectoryAsync(Lang.CrashDumpFileOutput);
        if (string.IsNullOrWhiteSpace(folder?.LocalPath))
            return;

        Setting.DumpFileDirPath = folder.LocalPath;
        ApplyChanges();
    }

    [RelayCommand]
    private void ThrowException()
    {
        _ = Task.Run(() => throw new Exception("Crash dump test exception."));
    }

}

