using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater;
using OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.ViewModels;

public partial class ProgramSettingViewModel : ObservableObject
{
    public ProgramSetting Setting => ProgramSetting.Default;

    [ObservableProperty]
    public partial bool EnableAssociateNyagekiProj { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableAssociateNyageki { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableAssociateOgkr { get; set; } = true;

    public ProgramSettingViewModel()
    {
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"program setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }

    public void ResetAllSettings()
    {
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
    }

    public async Task CheckUpdate()
    {
        await IoC.Get<IProgramUpdater>().CheckUpdatable();
    }

    public IProgramUpdater ProgramUpdater => IoC.Get<IProgramUpdater>();

    public void OpenShowNewVersionDialog()
    {
        IoC.Get<IWindowManager>().ShowWindowAsync(new ShowNewVersionDialogViewModel());
    }
}

