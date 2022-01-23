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
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    [Export(typeof(IAudioPlayerToolViewer))]
    public class AudioPlayerToolViewerViewModel : Tool, IAudioPlayerToolViewer
    {
        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

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
                Set(ref audioPlayer, value);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        private System.Action scrollAnimationClearFunc = default;

        public bool IsAudioButtonEnabled => AudioPlayer is not null;

        public AudioPlayerToolViewerViewModel()
        {
            DisplayName = "音频播放";
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
            {
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

            if (AudioPlayer.IsPlaying)
                OnPauseButtonClicked();
            else
                OnPlayOrResumeButtonClicked();
        }

        private void OnPauseButtonClicked()
        {
            AudioPlayer.Pause();
        }

        private void OnPlayOrResumeButtonClicked()
        {
            AudioPlayer.Play();
        }

        public void OnStopButtonClicked()
        {
            if (AudioPlayer is null)
                return;

            Editor.UnlockAllUserInteraction();
            scrollAnimationClearFunc?.Invoke();
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
    }
}
