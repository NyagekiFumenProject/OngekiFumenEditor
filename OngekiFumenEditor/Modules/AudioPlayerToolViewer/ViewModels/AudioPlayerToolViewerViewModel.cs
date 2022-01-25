using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Utils;
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
using System.Windows.Media;
using System.Windows.Media.Animation;

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

        private void OnAudioPlayerPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IAudioPlayer.CurrentTime))
                NotifyOfPropertyChange(() => SliderValue);
        }

        private System.Action scrollAnimationClearFunc = default;
        public bool IsAudioButtonEnabled => AudioPlayer is not null;
        private IFumenSoundPlayer fumenSoundPlayer;

        public AudioPlayerToolViewerViewModel()
        {
            DisplayName = "音频播放";
            fumenSoundPlayer = IoC.Get<IFumenSoundPlayer>();
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
            await fumenSoundPlayer.Init(Editor, AudioPlayer);
            (var timeline, var scrollViewer) = Editor.BeginScrollAnimation();
            EventHandler func = (e, d) =>
            {
                if (AudioPlayer is null || Editor is null)
                    return;
                scrollViewer.CurrentVerticalOffset = Math.Max(0, Editor.TotalDurationHeight - AudioPlayer.CurrentTime - Editor.CanvasHeight);
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
            var seekTo = SliderValue;
            Log.LogDebug($"Drag done, seek : {seekTo}");
            isSliderDragging = false;

            AudioPlayer.Seek(seekTo, false);
            fumenSoundPlayer.Seek(seekTo);
        }

        public void OnSliderValueStartChanged()
        {
            sliderDraggingValue = SliderValue;
            isSliderDragging = true;
            Log.LogDebug($"Begin drag, from : {SliderValue}");
        }
    }
}
