using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.ViewModels;

public partial class AudioSettingViewModel : ObservableObject
{
    public AudioSetting Setting => AudioSetting.Default;
    public AudioPlayerToolViewerSetting PlayerSetting => AudioPlayerToolViewerSetting.Default;

    public IEnumerable<AudioOutputType> AudioOutputTypeValues => Enum.GetValues<AudioOutputType>().OrderBy(x => x);

    public AudioSettingViewModel()
    {
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"audio setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
        PlayerSetting.Save();
    }
}

