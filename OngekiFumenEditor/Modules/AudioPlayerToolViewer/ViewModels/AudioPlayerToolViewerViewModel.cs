using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Utils;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    [Export(typeof(IAudioPlayerToolViewer))]
    public class AudioPlayerToolViewerViewModel : Tool, IAudioPlayerToolViewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private float sliderDraggingValue = 0;
        private bool isSliderDragging = false;
        public float SliderValue
        {
            get
            {
                var time = isSliderDragging ?
                sliderDraggingValue :
                (AudioPlayer?.CurrentTime ?? 0);
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
                AudioPlayer?.Dispose();
                fumenSoundPlayer?.Stop();
                scrollAnimationClearFunc?.Invoke();
                LoadAudio();
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        private IAudioPlayer audioPlayer = default;
        public IAudioPlayer AudioPlayer
        {
            get => audioPlayer;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(audioPlayer, value, OnAudioPlayerPropChanged);
                Set(ref audioPlayer, value);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        private IFumenSoundPlayer fumenSoundPlayer = default;
        public IFumenSoundPlayer FumenSoundPlayer
        {
            get => fumenSoundPlayer;
            set
            {
                Set(ref fumenSoundPlayer, value);
                var soundControl = FumenSoundPlayer.SoundControl;
                for (int i = 0; i < 12; i++)
                    SoundControls[i] = soundControl.HasFlag((SoundControl)(1 << i));
                NotifyOfPropertyChange(() => SoundControls);
            }
        }

        public bool[] SoundControls { get; set; } = new bool[13];

        public float SoundVolume
        {
            get => IoC.Get<IAudioManager>().SoundVolume;
            set
            {
                IoC.Get<IAudioManager>().SoundVolume = value;
                NotifyOfPropertyChange(() => SoundVolume);
            }
        }

        private void OnAudioPlayerPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IAudioPlayer.CurrentTime))
                NotifyOfPropertyChange(() => SliderValue);
        }

        private System.Action scrollAnimationClearFunc = default;
        public bool IsAudioButtonEnabled => AudioPlayer is not null;

        public AudioPlayerToolViewerViewModel()
        {
            DisplayName = "音频播放";
            FumenSoundPlayer = IoC.Get<IFumenSoundPlayer>();
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(FumenVisualEditorViewModel.EditorProjectData))
                return;
            Editor = Editor;
        }

        private async void LoadAudio()
        {
            if (string.IsNullOrWhiteSpace(Editor?.EditorProjectData?.AudioFilePath))
                return;
            var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(Editor.EditorProjectData.AudioFilePath);
            AudioPlayer = audioPlayer;
        }

        public void OnPlayOrPauseButtonClicked()
        {
            if (AudioPlayer is null)
                return;

            Editor.LockAllUserInteraction();
            if (scrollAnimationClearFunc is null)
                InitPreviewActions();

            if (AudioPlayer.IsPlaying)
                OnPauseButtonClicked();
            else
                OnPlayOrResumeButtonClicked();
        }

        private async void InitPreviewActions()
        {
            scrollAnimationClearFunc?.Invoke();
            await fumenSoundPlayer.Init(Editor, AudioPlayer);
            (var timeline, var scrollViewer) = Editor.BeginScrollAnimation();
            //var stopwatch = new Stopwatch();
            EventHandler func = (e, d) =>
            {
                if (AudioPlayer is null || Editor is null)
                    return;
                var audioTime = AudioPlayer.CurrentTime;
                NotifyOfPropertyChange(() => SliderValue);
                var scrollOffset = Editor.TotalDurationHeight - audioTime - Editor.CanvasHeight;
                scrollViewer.CurrentVerticalOffset = Math.Max(0, scrollOffset);
            };
            CompositionTarget.Rendering += func;
            scrollAnimationClearFunc = () =>
            {
                CompositionTarget.Rendering -= func;
                scrollAnimationClearFunc = default;
            };
        }

        private void OnPauseButtonClicked()
        {
            fumenSoundPlayer.Pause();
            AudioPlayer.Pause();
        }

        private void OnPlayOrResumeButtonClicked()
        {
            fumenSoundPlayer.Play();
            AudioPlayer.Play();
        }

        public void OnStopButtonClicked()
        {
            if (AudioPlayer is null)
                return;

            Editor.UnlockAllUserInteraction();
            scrollAnimationClearFunc?.Invoke();
            fumenSoundPlayer.Stop();
            AudioPlayer.Stop();
        }

        public void OnJumpButtonClicked()
        {
            if (AudioPlayer is null)
                return;

            //todo
        }

        public async void OnOpenFileButtonClicked()
        {
            var dialog = new OpenFileDialog();
            dialog.Multiselect = false;
            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                try
                {
                    AudioPlayer.Dispose();
                }
                catch
                {

                }
                try
                {
                    Editor.EditorProjectData.AudioFilePath = filePath;
                    var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(filePath);
                    AudioPlayer = audio;
                }
                catch (Exception e)
                {
                    var msg = $"无法打开音频文件:{filePath} ,原因:{e.Message}";
                    Log.LogError(msg);
                    MessageBox.Show(msg, "错误");
                }
            }
        }

        public void OnSliderValueChanged()
        {
            Log.LogDebug($"seek by OnSliderValueChanged()");

            if (scrollAnimationClearFunc is null)
                InitPreviewActions();
            var seekTo = SliderValue;
            AudioPlayer.Seek(seekTo, true);
            fumenSoundPlayer.Seek(seekTo, true);

            Log.LogDebug($"Drag done, seek : {seekTo}");
            isSliderDragging = false;
        }

        public void OnSliderValueStartChanged()
        {
            sliderDraggingValue = SliderValue;
            isSliderDragging = true;
            Log.LogDebug($"Begin drag, from : {SliderValue}");
        }

        public void RequestPlayOrPause()
        {
            if (AudioPlayer is null)
            {
                Log.LogWarn($"音频未加载!");
                return;
            }

            if (AudioPlayer.IsPlaying)
            {
                scrollAnimationClearFunc?.Invoke();
                fumenSoundPlayer.Pause();
                AudioPlayer.Pause();
            }
            else
            {
                if (scrollAnimationClearFunc is null)
                    InitPreviewActions();
                Log.LogDebug($"seek by RequestPlayOrPause()");
                var seekTo = (float)Editor.MinVisibleCanvasY;
                AudioPlayer.Seek(seekTo, false);
                fumenSoundPlayer.Seek(seekTo, false);
            }
        }

        public void OnSoundControlSwitchChanged(FrameworkElement sender)
        {
            var sc = 0;
            for (int i = 0; i < 13; i++)
                sc = sc | (SoundControls[i] ? (1 << i) : 0);
            if (FumenSoundPlayer is IFumenSoundPlayer player)
                player.SoundControl = (SoundControl)sc;

            //Log.LogDebug($"Apply sound control:{(SoundControl)sc}");
            NotifyOfPropertyChange(() => SoundControls);
        }
    }
}
