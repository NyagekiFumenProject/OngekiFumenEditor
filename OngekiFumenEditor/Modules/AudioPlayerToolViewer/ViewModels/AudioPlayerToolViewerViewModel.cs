using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;

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
                LoadAudio();
                Animator = value?.BeginScrollAnimation();
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
                if (Animator is not null)
                    Animator.WrapCore = value;
            }
        }

        private FumenScrollViewerAnimationWrapper animator = default;
        public FumenScrollViewerAnimationWrapper Animator
        {
            get => animator;
            set
            {
                Set(ref animator, value);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
                if (AudioPlayer is not null && Animator is not null)
                    Animator.WrapCore = AudioPlayer;
            }
        }

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
            if (AudioPlayer is not IAudioPlayer player)
                return;

            if (Animator.IsPlaying)
                Animator.Pause();
            else
                Animator.Play();
        }

        public void OnStopButtonClicked()
        {
            if (AudioPlayer is not IAudioPlayer player)
                return;

            Animator.Stop();
        }

        public void OnJumpButtonClicked()
        {
            if (AudioPlayer is not IAudioPlayer player)
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
                    Animator?.Stop();
                    Animator?.Dispose();
                }
                catch
                {

                }
                try
                {
                    Editor.EditorProjectData.AudioFilePath = filePath;
                    var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(filePath);
                    AudioPlayer = audio;
                    Animator.WrapCore = audio;
                    NotifyOfPropertyChange(() => IsAudioButtonEnabled);
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
