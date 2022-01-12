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
        private Dictionary<FumenVisualEditorViewModel, EditorBindingModel> currentHoldingEditorModelMap = new();

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
                editor = value;
                NotifyOfPropertyChange(() => Editor);
                if (!currentHoldingEditorModelMap.TryGetValue(Editor, out var binding))
                {
                    binding = new EditorBindingModel();
                    currentHoldingEditorModelMap[Editor] = binding;
                }

                EditorBinding = binding;
            }
        }

        private EditorBindingModel editorBinding = new EditorBindingModel();
        public EditorBindingModel EditorBinding
        {
            get => editorBinding;
            set
            {
                Set(ref editorBinding, value);
                NotifyOfPropertyChange(() => IsAudioButtonEnabled);
            }
        }

        public bool IsAudioButtonEnabled => EditorBinding?.AudioPlayer is not null;

        public void OnPlayOrPauseButtonClicked()
        {
            if (EditorBinding?.AudioPlayer is not IAudioPlayer player)
                return;

            if (player.IsPlaying)
                player.Pause();
            else
                player.Play();
        }

        public void OnStopButtonClicked()
        {
            if (EditorBinding?.AudioPlayer is not IAudioPlayer player)
                return;

            player.Stop();
        }

        public void OnJumpButtonClicked()
        {
            if (EditorBinding?.AudioPlayer is not IAudioPlayer player)
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
                    EditorBinding.AudioPlayer?.Stop();
                    EditorBinding.AudioPlayer?.Dispose();
                    EditorBinding.AudioName = string.Empty;
                }
                catch
                {

                }
                try
                {
                    var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(filePath);
                    EditorBinding.AudioPlayer = audio;
                    EditorBinding.AudioName = Path.GetFileNameWithoutExtension(filePath);
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
