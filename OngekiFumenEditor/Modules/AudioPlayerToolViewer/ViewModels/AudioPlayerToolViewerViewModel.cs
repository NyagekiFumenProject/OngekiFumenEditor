using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    [Export(typeof(IAudioPlayerToolViewer))]
    public partial class AudioPlayerToolViewerViewModel : Tool, IAudioPlayerToolViewer, IDisposable
    {
        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private float sliderDraggingValue = 0;
        private bool isSliderDragging = false;
        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly CancellationTokenSource lifetimeCancellation = new();
        private int disposed;
        private bool runtimeEventsAttached;

        internal bool IsDisposed => Volatile.Read(ref disposed) != 0;
        internal CancellationToken LifetimeCancellation => lifetimeCancellation.Token;
        public float SliderValue
        {
            get
            {
                var time = isSliderDragging ?
                sliderDraggingValue :
                (float)(AudioPlayer?.CurrentTime.TotalMilliseconds ?? 0);
                return time;
            }
            set
            {
                if (isSliderDragging)
                    sliderDraggingValue = value;
                NotifyOfPropertyChange(() => SliderValue);
            }
        }

        private FumenVisualEditorViewModel editor = default;
        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                Set(ref editor, value);
                if (FumenSoundPlayer is not null)
                    _ = ObserveOperationAsync(FumenSoundPlayer.Clean(), "clean editor sound");
                AudioPlayer = Editor?.AudioPlayer;
            }
        }

        private IAudioPlayer audioPlayer;
        public IAudioPlayer AudioPlayer
        {
            get => audioPlayer;
            private set
            {
                if (IsDisposed)
                {
                    value?.OnPlaybackFinished -= OnPlaybackFinished;
                    return;
                }

                if (audioPlayer is not null)
                    audioPlayer.OnPlaybackFinished -= OnPlaybackFinished;
                Set(ref audioPlayer, value);
                if (audioPlayer is not null)
                    audioPlayer.OnPlaybackFinished += OnPlaybackFinished;

                if (AudioPlayer is null)
                    CleanWaveform();
                else
                    PrepareWaveform(AudioPlayer);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        private void OnPlaybackFinished()
        {
            if (IsDisposed)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.HasShutdownStarted)
                return;

            dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (IsDisposed)
                    return;

                Log.LogInfo($"OnPlaybackFinished()~~");
                OnStopButtonClicked();
                if (AudioPlayer is not null && Editor is not null)
                {
                    var audioTime = AudioPlayer.Duration - TimeSpan.FromSeconds(1);
                    Editor.ScrollTo(audioTime);
                }
            }));
        }

        private IFumenSoundPlayer fumenSoundPlayer = default;
        private TimeSpan playStartTime;

        public IFumenSoundPlayer FumenSoundPlayer
        {
            get => fumenSoundPlayer;
            set
            {
                Set(ref fumenSoundPlayer, value);

                //init SoundControls
                var soundControl = FumenSoundPlayer.SoundControl;
                var length = Enum.GetValues<SoundControl>().Length;
                for (int i = 0; i < length; i++)
                    SoundControls[i] = soundControl.HasFlag((SoundControl)(1 << i));
                NotifyOfPropertyChange(() => SoundControls);

                //init SoundVolumes
                var sounds = Enum.GetValues<SoundControl>();
                SoundVolumes = sounds.Select(x => new SoundVolumeProxy(value, x)).Where(x => x.IsValid).ToArray();
                NotifyOfPropertyChange(() => SoundVolumes);
            }
        }

        public bool[] SoundControls { get; set; } = new bool[Enum.GetValues<SoundControl>().Length];

        public SoundVolumeProxy[] SoundVolumes { get; set; } = [];

        public float SoundVolume
        {
            get => IoC.Get<IAudioManager>().SoundVolume;
            set
            {
                IoC.Get<IAudioManager>().SoundVolume = value;
                NotifyOfPropertyChange(() => SoundVolume);
            }
        }

        public float MusicVolume
        {
            get => IoC.Get<IAudioManager>().MusicVolume;
            set
            {
                IoC.Get<IAudioManager>().MusicVolume = value;
                NotifyOfPropertyChange(() => MusicVolume);
            }
        }

        public float MusicSpeed
        {
            get => IoC.Get<IAudioManager>().MusicSpeed;
            set
            {
                IoC.Get<IAudioManager>().MusicSpeed = value;
                NotifyOfPropertyChange(() => MusicSpeed);
            }
        }

        public bool IsAudioButtonEnabled => AudioPlayer is not null;

        public AudioPlayerToolViewerViewModel()
        {
            DisplayName = Resources.AudioPlayerToolViewer;
            FumenSoundPlayer = IoC.Get<IFumenSoundPlayer>();
            editorDocumentManager = IoC.Get<IEditorDocumentManager>();
            PropertyChanged += OnToolPropertyChanged;
            AttachRuntimeEvents();
            Editor = editorDocumentManager.CurrentActivatedEditor;
            this.RegisterOrUnregisterPropertyChangeEvent(null, editor, OnEditorPropertyChanged);

            UpdateActualRenderInterval();
        }

        private void OnToolPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IsVisible) || IsDisposed)
                return;

            if (IsVisible)
                ResumeRuntime();
            else
            {
                SuspendRuntime();
            }
        }

        private void AttachRuntimeEvents()
        {
            if (IsDisposed || runtimeEventsAttached)
                return;

            runtimeEventsAttached = true;
            editorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        private void DetachRuntimeEvents()
        {
            if (!runtimeEventsAttached && editor is null && audioPlayer is null)
                return;

            editorDocumentManager.OnActivateEditorChanged -= OnActivateEditorChanged;
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            this.RegisterOrUnregisterPropertyChangeEvent(editor, null, OnEditorPropertyChanged);

            if (audioPlayer is not null)
            {
                audioPlayer.OnPlaybackFinished -= OnPlaybackFinished;
                audioPlayer = null;
            }

            editor = null;
            runtimeEventsAttached = false;
        }

        private void SuspendRuntime()
        {
            if (IsDisposed)
                return;

            DetachRuntimeEvents();
            FumenSoundPlayer?.Stop();
            if (FumenSoundPlayer is not null)
                _ = ObserveOperationAsync(FumenSoundPlayer.Clean(), "clean hidden sound");
            SuspendWaveformRuntime();
        }

        private void ResumeRuntime()
        {
            if (IsDisposed)
                return;

            AttachRuntimeEvents();
            Editor = editorDocumentManager.CurrentActivatedEditor;
            this.RegisterOrUnregisterPropertyChangeEvent(null, editor, OnEditorPropertyChanged);
            ResumeWaveformRuntime();
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            if (IsDisposed)
                return;

            Editor = @new;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsDisposed || !runtimeEventsAttached)
                return;

            switch (e.PropertyName)
            {
                case nameof(FumenVisualEditorViewModel.EditorProjectData):
                    Editor = Editor;
                    break;
                case nameof(FumenVisualEditorViewModel.AudioPlayer):
                    AudioPlayer = Editor?.AudioPlayer;
                    break;
                default:
                    break;
            }
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            if (AudioPlayer is null)
                return;
            if (!AudioPlayer.IsPlaying)
                return;
            Process(AudioPlayer.CurrentTime);
        }

        private void Process(TimeSpan time)
        {
            if (Editor is null)
                return;
            NotifyOfPropertyChange(() => SliderValue);
            var tGrid = Editor.ConvertAudioTimeToTGrid(time);
            Editor.ScrollTo(tGrid);
        }

        public void OnStopButtonClicked()
        {
            //Editor.UnlockAllUserInteraction();
            FumenSoundPlayer?.Stop();
            AudioPlayer?.Stop();
            if (EditorGlobalSetting.Default.ReturnStartTimeAfterPause)
            {
                //recovery editor current time
                Process(playStartTime);
            }
        }

        public void OnSliderValueStartChanged()
        {
            sliderDraggingValue = SliderValue;
            isSliderDragging = true;
            Log.LogDebug($"Begin drag, from : {SliderValue}");
        }

        public void RequestPlayOrPause()
        {
            _ = ObserveOperationAsync(RequestPlayOrPauseAsync(), "play/pause");
        }

        private async Task RequestPlayOrPauseAsync()
        {
            if (IsDisposed)
                return;

            if (AudioPlayer is null)
            {
                Log.LogWarn($"音频未加载!");
                return;
            }
            if (!AudioPlayer.IsAvaliable)
            {
                Log.LogWarn($"音频还没准备好!");
                return;
            }
            if (AudioPlayer.IsPlaying)
            {
                OnStopButtonClicked();
            }
            else
            {
                var player = AudioPlayer;
                var editor = Editor;
                if (player is null || editor is null || FumenSoundPlayer is null)
                    return;

                await FumenSoundPlayer.Prepare(editor, player);
                if (IsDisposed || !ReferenceEquals(player, AudioPlayer) || !ReferenceEquals(editor, Editor))
                    return;

                var tgrid = editor.GetCurrentTGrid();
                var seekTo = editor.ConvertTGridToAudioTime(tgrid);
                Log.LogDebug($"seek to {tgrid}({seekTo})");
                player.Seek(seekTo, false);
                FumenSoundPlayer.Seek(seekTo, false);
                playStartTime = seekTo;
            }
        }

        public void OnSoundControlSwitchChanged(FrameworkElement sender)
        {
            var sc = 0;
            var length = Enum.GetValues<SoundControl>().Length;
            for (int i = 0; i < length; i++)
                sc = sc | (SoundControls[i] ? (1 << i) : 0);
            if (FumenSoundPlayer is IFumenSoundPlayer player)
                player.SoundControl = (SoundControl)sc;

            //持久化音效开关，下次启动/重开面板沿用
            AudioPlayerToolViewerSetting.Default.SoundControlMask = sc;
            AudioPlayerToolViewerSetting.Default.Save();

            //Log.LogDebug($"Apply sound control:{(SoundControl)sc}");
            NotifyOfPropertyChange(() => SoundControls);
        }

        public void OnReloadSoundFiles()
        {
            _ = ObserveOperationAsync(ReloadSoundFilesAsync(), "reload sound files");
        }

        private async Task ReloadSoundFilesAsync()
        {
            if (IsDisposed)
                return;

            if (AudioPlayer is null || FumenSoundPlayer is null)
            {
                MessageBox.Show(Resources.WaitForAudioAndFumenLoaded);
                return;
            }

            if (AudioPlayer.IsPlaying)
            {
                MessageBox.Show(Resources.PauseAudioAndFumen);
                return;
            }

            var soundPlayer = FumenSoundPlayer;
            var result = await soundPlayer.ReloadSoundFiles();

            if (!IsDisposed && result)
            {
                MessageBox.Show(Resources.SoundLoaded);
            }
        }

        private async Task ObserveOperationAsync(Task operation, string operationName)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Log.LogError($"Audio player tool {operationName} failed.", e);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            lifetimeCancellation.Cancel();
            PropertyChanged -= OnToolPropertyChanged;
            DetachRuntimeEvents();

            editor = null;

            var soundPlayer = FumenSoundPlayer;
            soundPlayer?.Stop();
            if (soundPlayer is not null)
                _ = ObserveOperationAsync(soundPlayer.Clean(), "clean sound");
            DisposeWaveformBlocks();
        }
    }
}
