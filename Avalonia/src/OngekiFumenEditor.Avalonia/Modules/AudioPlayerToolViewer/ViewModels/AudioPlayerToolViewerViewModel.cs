using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.ViewModels;

[RegisterSingleton<IAudioPlayerToolViewer>]
public partial class AudioPlayerToolViewerViewModel : ToolViewModelBase, IAudioPlayerToolViewer
{
    private static readonly SoundControl[] IndividualSoundControls = Enum.GetValues<SoundControl>()
        .Where(x => x != SoundControl.All)
        .ToArray();

    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly IAudioManager audioManager;
    private readonly DispatcherTimer playbackSyncTimer;
    private TimeSpan playStartTime;
    private bool isDisposed;
    private readonly ILogger<AudioPlayerToolViewerViewModel> logger;

    private FumenVisualEditorViewModel editor;
    public FumenVisualEditorViewModel Editor
    {
        get => editor;
        private set
        {
            if (ReferenceEquals(editor, value))
            {
                AudioPlayer = value?.AudioPlayer;
                return;
            }

            SetProperty(ref editor, value);
            _ = CleanSoundPlayerAsync();
            AudioPlayer = value?.AudioPlayer;
        }
    }

    private IAudioPlayer audioPlayer;
    public IAudioPlayer AudioPlayer
    {
        get => audioPlayer;
        private set
        {
            if (ReferenceEquals(audioPlayer, value))
                return;

            if (audioPlayer is not null)
                audioPlayer.OnPlaybackFinished -= OnPlaybackFinished;

            SetProperty(ref audioPlayer, value);

            if (audioPlayer is not null)
                audioPlayer.OnPlaybackFinished += OnPlaybackFinished;

            OnWaveformAudioPlayerChanged(audioPlayer);
            OnPropertyChanged(nameof(IsAudioButtonEnabled));
        }
    }

    private IFumenSoundPlayer fumenSoundPlayer;
    public IFumenSoundPlayer FumenSoundPlayer
    {
        get => fumenSoundPlayer;
        private set
        {
            if (!SetProperty(ref fumenSoundPlayer, value))
                return;

            if (value is null)
            {
                SoundControls = new bool[IndividualSoundControls.Length];
                SoundVolumes = [];
            }
            else
            {
                SoundControls = IndividualSoundControls
                    .Select(x => value.SoundControl.HasFlag(x))
                    .ToArray();
                SoundVolumes = IndividualSoundControls
                    .Select(x => new SoundVolumeProxy(value, x))
                    .Where(x => x.IsValid)
                    .ToArray();
            }

            OnPropertyChanged(nameof(SoundControls));
            OnPropertyChanged(nameof(SoundVolumes));
        }
    }

    public bool[] SoundControls { get; private set; } = new bool[IndividualSoundControls.Length];
    public SoundVolumeProxy[] SoundVolumes { get; private set; } = [];

    private float soundVolume = 1;
    public float SoundVolume
    {
        get => audioManager?.SoundVolume ?? soundVolume;
        set
        {
            soundVolume = value;
            if (audioManager is not null)
                audioManager.SoundVolume = value;
            OnPropertyChanged();
        }
    }

    private float musicVolume = 1;
    public float MusicVolume
    {
        get => audioManager?.MusicVolume ?? musicVolume;
        set
        {
            musicVolume = value;
            if (audioManager is not null)
                audioManager.MusicVolume = value;
            OnPropertyChanged();
        }
    }

    private float musicSpeed = 1;
    public float MusicSpeed
    {
        get => audioManager?.MusicSpeed ?? musicSpeed;
        set
        {
            musicSpeed = value;
            if (audioManager is not null)
                audioManager.MusicSpeed = value;
            OnPropertyChanged();
        }
    }

    public bool IsAudioButtonEnabled => AudioPlayer is not null;

    public bool CanAdjustMusicSpeed => audioManager?.EnableVarspeed == true;

    private IWaveformDrawing waveformDrawing;
    public IWaveformDrawing WaveformDrawing
    {
        get => waveformDrawing;
        private set => SetProperty(ref waveformDrawing, value);
    }

    private int resampleSize = AudioPlayerToolViewerSetting.Default.ResampleSize;
    public int ResampleSize
    {
        get => resampleSize;
        set
        {
            if (!SetProperty(ref resampleSize, value))
                return;

            OnWaveformResampleSizeChanged();
            AudioPlayerToolViewerSetting.Default.ResampleSize = value;
            AudioPlayerToolViewerSetting.Default.Save();
        }
    }

    private float waveformVecticalScale = AudioPlayerToolViewerSetting.Default.WaveformVecticalScale;
    public float WaveformVecticalScale
    {
        get => waveformVecticalScale;
        set
        {
            if (!SetProperty(ref waveformVecticalScale, value))
                return;

            AudioPlayerToolViewerSetting.Default.WaveformVecticalScale = value;
            AudioPlayerToolViewerSetting.Default.Save();
        }
    }

    private float durationMsPerPixel = AudioPlayerToolViewerSetting.Default.DurationMsPerPixel;
    public float DurationMsPerPixel
    {
        get => durationMsPerPixel;
        set
        {
            if (!SetProperty(ref durationMsPerPixel, value))
                return;

            AudioPlayerToolViewerSetting.Default.DurationMsPerPixel = value;
            AudioPlayerToolViewerSetting.Default.Save();
        }
    }

    private float currentTimeXOffset = AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset;
    public float CurrentTimeXOffset
    {
        get => currentTimeXOffset;
        set
        {
            if (!SetProperty(ref currentTimeXOffset, value))
                return;

            AudioPlayerToolViewerSetting.Default.CurrentTimeXOffset = value;
            AudioPlayerToolViewerSetting.Default.Save();
        }
    }

    private int limitFPS = AudioPlayerToolViewerSetting.Default.LimitFPS;
    public int LimitFPS
    {
        get => limitFPS;
        set
        {
            if (!SetProperty(ref limitFPS, value))
                return;

            AudioPlayerToolViewerSetting.Default.LimitFPS = value;
            AudioPlayerToolViewerSetting.Default.Save();
        }
    }

    private bool isShowWaveform = true;
    public bool IsShowWaveform
    {
        get => isShowWaveform && AudioPlayerToolViewerSetting.Default.EnableWaveformDisplay;
        set => SetProperty(ref isShowWaveform, value);
    }

    public AudioPlayerToolViewerViewModel(ILogger<AudioPlayerToolViewerViewModel> logger) : base(Lang.B.AudioPlayerToolViewer.ToLocalizedString())
    {
        this.logger = logger;
        Dock = global::Dock.Model.Core.DockMode.Bottom;

        WaveformDrawing = TryGetService<IWaveformDrawing>();
        audioManager = TryGetService<IAudioManager>();
        if (audioManager is not null)
        {
            soundVolume = audioManager.SoundVolume;
            musicVolume = audioManager.MusicVolume;
            musicSpeed = audioManager.MusicSpeed;
            FumenSoundPlayer = TryGetService<IFumenSoundPlayer>();
        }

        editorDocumentManager = TryGetService<IEditorDocumentManager>();
        if (editorDocumentManager is not null)
        {
            editorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
            OnActivateEditorChanged(editorDocumentManager.CurrentActivatedEditor, null);
        }

        // 对齐 WPF CompositionTarget.Rendering -> Process()：播放中把音频时间同步到编辑器时间轴。
        playbackSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        playbackSyncTimer.Tick += OnPlaybackSyncTick;
        playbackSyncTimer.Start();
    }

    private void OnPlaybackSyncTick(object sender, EventArgs e)
    {
        var player = AudioPlayer;
        var currentEditor = Editor;
        if (player is null || currentEditor is null || !player.IsPlaying)
            return;

        var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(player.CurrentTime, currentEditor);
        currentEditor.ScrollTo(tGrid);
    }

    private static T TryGetService<T>() where T : class
    {
        try
        {
            return IoC.Get<T>();
        }
        catch (InvalidOperationException e)
        {
            if (!Design.IsDesignMode)
                Log.LogWarn(e.Message);
            return null;
        }
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        Editor = @new;
    }

    private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FumenVisualEditorViewModel.EditorContext)
            or nameof(FumenVisualEditorViewModel.AudioPlayer))
        {
            AudioPlayer = Editor?.AudioPlayer;
        }
    }

    private void OnPlaybackFinished()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Log.LogInfo("Audio playback finished.");
            StopPlayback();

            if (AudioPlayer is not null && Editor is not null)
                Editor.ScrollTo(AudioPlayer.Duration - TimeSpan.FromSeconds(1));
        });
    }

    private async Task CleanSoundPlayerAsync()
    {
        if (FumenSoundPlayer is null)
            return;

        try
        {
            await FumenSoundPlayer.Clean();
        }
        catch (Exception e)
        {
            Log.LogError("Failed to clean the fumen sound player.", e);
        }
    }

    private void StopPlayback()
    {
        //Editor.UnlockAllUserInteraction();
        logger.LogInformation("Playback stopped.");
        FumenSoundPlayer?.Stop();
        AudioPlayer?.Stop();

        if (EditorGlobalSetting.Default.ReturnStartTimeAfterPause && Editor is not null)
            Editor.ScrollTo(playStartTime);
    }

    public void RequestPlayOrPause()
    {
        _ = RequestPlayOrPauseAsync();
    }

    private async Task RequestPlayOrPauseAsync()
    {
        try
        {
            if (AudioPlayer is null)
            {
                Log.LogWarn("音频未加载!");
                return;
            }

            if (!AudioPlayer.IsAvaliable)
            {
                Log.LogWarn("音频还没准备好!");
                return;
            }

            if (AudioPlayer.IsPlaying)
            {
                logger.LogInformation("Playback paused by user.");
                StopPlayback();
                return;
            }

            if (FumenSoundPlayer is null || Editor is null)
            {
                Log.LogWarn("Audio or fumen sound backend is not initialized.");
                return;
            }

            await FumenSoundPlayer.Prepare(Editor, AudioPlayer);
            var tGrid = Editor.GetCurrentTGrid();
            var seekTo = TGridCalculator.ConvertTGridToAudioTime(tGrid, Editor);
            logger.LogInformation("Play requested: seek to {TGrid}({SeekTo}).", tGrid, seekTo);
            AudioPlayer.Seek(seekTo, false);
            FumenSoundPlayer.Seek(seekTo, false);
            playStartTime = seekTo;
        }
        catch (Exception e)
        {
            Log.LogError("Failed to change audio playback state.", e);
        }
    }

    public void OnSoundControlSwitchChanged()
    {
        var soundControl = (SoundControl)0;
        for (var i = 0; i < Math.Min(SoundControls.Length, IndividualSoundControls.Length); i++)
        {
            if (SoundControls[i])
                soundControl |= IndividualSoundControls[i];
        }

        if (FumenSoundPlayer is not null)
            FumenSoundPlayer.SoundControl = soundControl;

        logger.LogInformation("Sound control switches changed to {SoundControl}.", soundControl);
        OnPropertyChanged(nameof(SoundControls));
    }

    [RelayCommand]
    private void ResetWaveformOptions()
    {
        logger.LogInformation("ResetWaveformOptions triggered.");
        WaveformDrawing?.Options?.Reset();
    }

    [RelayCommand]
    private void SaveWaveformOptions()
    {
        logger.LogInformation("SaveWaveformOptions triggered.");
        WaveformDrawing?.Options?.Save();
    }

    [RelayCommand]
    private async Task ReloadSoundFilesAsync()
    {
        logger.LogInformation("ReloadSoundFiles triggered.");
        if (AudioPlayer is null || FumenSoundPlayer is null)
        {
            await ShowMessageAsync(Lang.WaitForAudioAndFumenLoaded);
            return;
        }

        if (AudioPlayer.IsPlaying)
        {
            await ShowMessageAsync(Lang.PauseAudioAndFumen);
            return;
        }

        if (await FumenSoundPlayer.ReloadSoundFiles())
            await ShowMessageAsync(Lang.SoundLoaded);
    }

    private static Task ShowMessageAsync(string message)
    {
        return IoC.Get<IDialogManager>().ShowMessageDialog(message);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        playbackSyncTimer.Stop();
        playbackSyncTimer.Tick -= OnPlaybackSyncTick;
        if (editorDocumentManager is not null)
            editorDocumentManager.OnActivateEditorChanged -= OnActivateEditorChanged;
        if (Editor is not null)
            Editor.PropertyChanged -= OnEditorPropertyChanged;
        if (AudioPlayer is not null)
            AudioPlayer.OnPlaybackFinished -= OnPlaybackFinished;

        FumenSoundPlayer?.Stop();
        _ = CleanSoundPlayerAsync();
        DisposeWaveformRendering();
    }
}
