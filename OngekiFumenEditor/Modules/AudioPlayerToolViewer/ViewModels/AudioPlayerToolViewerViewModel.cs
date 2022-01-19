using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models;
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
        public AudioPlayerToolViewerViewModel()
        {
            DisplayName = "音频播放";
        }

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
            }
        }

        private async void LoadAudio()
        {
            if (string.IsNullOrWhiteSpace(Editor?.EditorProjectData?.AudioFilePath))
                return;
            var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(Editor.EditorProjectData.AudioFilePath);
            AudioPlayer = audioPlayer;
        }

        private IAudioPlayer audioPlayer = default;
        public IAudioPlayer AudioPlayer
        {
            get => audioPlayer;
            set {
                Set(ref audioPlayer, value);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        public bool IsAudioButtonEnabled => AudioPlayer is not null;

        public void OnPlayOrPauseButtonClicked()
        {
            if (AudioPlayer is not IAudioPlayer player)
                return;

            if (player.IsPlaying)
                player.Pause();
            else
                player.Play();
        }

        public void OnStopButtonClicked()
        {
            if (AudioPlayer is not IAudioPlayer player)
                return;

            player.Stop();
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
                    AudioPlayer?.Stop();
                    AudioPlayer?.Dispose();
                }
                catch
                {

                }
                try
                {
                    Editor.EditorProjectData.AudioFilePath = filePath;
                    var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(filePath);
                    AudioPlayer = audio;
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
