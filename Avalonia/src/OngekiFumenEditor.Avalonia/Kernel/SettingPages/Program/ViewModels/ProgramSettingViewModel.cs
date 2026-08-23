using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Settings;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class ProgramSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly ILogger<ProgramSettingViewModel> logger;

    public ProgramSetting Setting => ProgramSetting.Default;

    public string SettingsPageName => Lang.TabProgram;

    public string SettingsPagePath => Lang.TabEnviorment;

    public ProgramSettingViewModel(ILogger<ProgramSettingViewModel> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"program setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }

    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        logger.LogInformation("ResetAllSettingsAsync triggered.");
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
        logger.LogInformation("SelectDumpFolderAsync triggered.");
        using var folder = await FileDialogHelper.OpenDirectoryAsync(Lang.CrashDumpFileOutput);
        if (string.IsNullOrWhiteSpace(folder?.LocalPath))
            return;

        Setting.DumpFileDirPath = folder.LocalPath;
        ApplyChanges();
    }

    [RelayCommand]
    private void ThrowException()
    {
        logger.LogInformation("ThrowException triggered.");
        _ = Task.Run(() => throw new Exception("Crash dump test exception."));
    }

}

