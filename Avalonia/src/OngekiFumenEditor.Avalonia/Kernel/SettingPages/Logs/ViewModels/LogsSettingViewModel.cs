using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.ViewModels;

public partial class LogsSettingViewModel : ObservableObject
{
    public LogSetting Setting => LogSetting.Default;

    public LogsSettingViewModel()
    {
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"logs setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }
}

