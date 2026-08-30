using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Settings;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class AudioSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly IAudioPlatformCapabilities platformCapabilities;
    private readonly AudioSetting setting;
    private readonly AudioPlayerToolViewerSetting playerSetting;
    private readonly Action saveSettings;
    private AudioOutputResolution outputResolution;
    private AudioOutputType selectedAudioOutputType;

    public AudioSetting Setting => setting;
    public AudioPlayerToolViewerSetting PlayerSetting => playerSetting;

    public string SettingsPageName => Lang.TabAudio;

    public string SettingsPagePath => Lang.TabSound;

    public IReadOnlyList<AudioOutputType> AudioOutputTypeValues => platformCapabilities.SelectableOutputTypes;

    public bool CanSelectAudioOutput => platformCapabilities.CanSelectOutputBackend;

    public bool HasFixedAudioOutput => !CanSelectAudioOutput;

    public bool SupportsVarspeed => platformCapabilities.SupportsVarspeed;

    public bool HasAudioOutputFallback => outputResolution.IsFallback &&
                                          outputResolution.EffectiveBackend is not AudioBackendKind.None;

    public string FixedAudioOutputDescription =>
        Lang.AudioOutputFixedBackend.Format(GetBackendName(platformCapabilities.DefaultBackend));

    public string AudioOutputFallbackMessage => HasAudioOutputFallback
        ? Lang.AudioOutputFallback.Format(outputResolution.RequestedOutput, outputResolution.EffectiveBackendName)
        : string.Empty;

    public AudioOutputType SelectedAudioOutputType
    {
        get => selectedAudioOutputType;
        set
        {
            if (!platformCapabilities.SelectableOutputTypes.Contains(value) ||
                !SetProperty(ref selectedAudioOutputType, value))
            {
                return;
            }

            outputResolution = platformCapabilities.ResolveOutput(value);
            OnPropertyChanged(nameof(HasAudioOutputFallback));
            OnPropertyChanged(nameof(AudioOutputFallbackMessage));
        }
    }

    public AudioSettingViewModel()
        : this(ResolvePlatformCapabilities(),
            AudioSetting.Default,
            AudioPlayerToolViewerSetting.Default,
            saveSettings: null)
    {
    }

    internal AudioSettingViewModel(
        IAudioPlatformCapabilities platformCapabilities,
        AudioSetting setting,
        AudioPlayerToolViewerSetting playerSetting,
        Action saveSettings)
    {
        this.platformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
        this.setting = setting ?? throw new ArgumentNullException(nameof(setting));
        this.playerSetting = playerSetting ?? throw new ArgumentNullException(nameof(playerSetting));
        this.saveSettings = saveSettings ?? (() =>
        {
            this.setting.Save();
            this.playerSetting.Save();
        });
        LoadOutputSelection();
        Setting.PropertyChanged += OnSettingPropertyChanged;
    }

    public void ApplyChanges()
    {
        if (CanSelectAudioOutput)
            Setting.AudioOutputType = (int)SelectedAudioOutputType;

        saveSettings();
    }

    private void OnSettingPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Log.LogDebug($"audio setting property changed : {e.PropertyName}");
        if (e.PropertyName is nameof(AudioSetting.AudioOutputType))
            LoadOutputSelection();
    }

    private void LoadOutputSelection()
    {
        var requestedOutput = (AudioOutputType)Setting.AudioOutputType;
        outputResolution = platformCapabilities.ResolveOutput(requestedOutput);
        selectedAudioOutputType = outputResolution.EffectiveBackend switch
        {
            AudioBackendKind.Asio => AudioOutputType.Asio,
            _ => AudioOutputType.Wasapi
        };

        OnPropertyChanged(nameof(SelectedAudioOutputType));
        OnPropertyChanged(nameof(HasAudioOutputFallback));
        OnPropertyChanged(nameof(AudioOutputFallbackMessage));
    }

    private static IAudioPlatformCapabilities ResolvePlatformCapabilities()
    {
        try
        {
            return IoC.Get<IAudioPlatformCapabilities>();
        }
        catch (InvalidOperationException e)
        {
            if (!Design.IsDesignMode)
                Log.LogWarning(e.Message);
            return AudioPlatformCapabilities.Unknown;
        }
    }

    private static string GetBackendName(AudioBackendKind backend) => backend switch
    {
        AudioBackendKind.Wasapi => "WASAPI",
        AudioBackendKind.Asio => "ASIO",
        AudioBackendKind.BrowserAudioWorklet => "Browser AudioWorklet",
        _ => Lang.AudioOutputUnavailable
    };

    [RelayCommand]
    private async Task SelectSoundFolderAsync()
    {
        Log.LogInfo("SelectSoundFolderAsync triggered.");
        using var folder = await FileDialogHelper.OpenDirectoryAsync(Lang.SoundFolderPath);
        if (string.IsNullOrWhiteSpace(folder?.FullPath))
            return;

        Setting.SoundFolderPath = folder.FullPath;
        ApplyChanges();
    }
}

