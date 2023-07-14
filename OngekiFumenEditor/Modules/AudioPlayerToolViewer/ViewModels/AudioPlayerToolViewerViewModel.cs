using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Utils;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static OngekiFumenEditor.Kernel.Audio.DefaultCommonImpl.Sound.DefaultFumenSoundPlayer;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    [Export(typeof(IAudioPlayerToolViewer))]
    public partial class AudioPlayerToolViewerViewModel : Tool, IAudioPlayerToolViewer
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
                FumenSoundPlayer?.Clean();
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
                PrepareWaveform(AudioPlayer);
            }
        }

        const int SoundControlLength = 16;

        private IFumenSoundPlayer fumenSoundPlayer = default;
        public IFumenSoundPlayer FumenSoundPlayer
        {
            get => fumenSoundPlayer;
            set
            {
                Set(ref fumenSoundPlayer, value);

                //init SoundControls
                var soundControl = FumenSoundPlayer.SoundControl;
                for (int i = 0; i < SoundControlLength; i++)
                    SoundControls[i] = soundControl.HasFlag((SoundControl)(1 << i));
                NotifyOfPropertyChange(() => SoundControls);

                //init SoundVolumes
                var sounds = Enum.GetValues<SoundControl>();
                SoundVolumes = sounds.Select(x => new SoundVolumeProxy(value, x)).Where(x=>x.IsValid).ToArray();
                NotifyOfPropertyChange(() => SoundVolumes);
            }
        }

        public bool[] SoundControls { get; set; } = new bool[SoundControlLength];

        public SoundVolumeProxy[] SoundVolumes { get; set; } = new SoundVolumeProxy[0];

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
            {
                AudioPlayer?.Dispose();
                AudioPlayer = null;
                return;
            }
            var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(Editor.EditorProjectData.AudioFilePath);
            AudioPlayer = audioPlayer;
        }

        private async void InitPreviewActions()
        {
            scrollAnimationClearFunc?.Invoke();
            await FumenSoundPlayer.Prepare(Editor, AudioPlayer);
            EventHandler func = (e, d) =>
            {
                if (AudioPlayer is null)
                    return;
                Process(AudioPlayer.CurrentTime);
            };
            CompositionTarget.Rendering += func;
            scrollAnimationClearFunc = () =>
            {
                CompositionTarget.Rendering -= func;
                scrollAnimationClearFunc = default;
            };
        }

        private void Process(TimeSpan time)
        {
            if (Editor is null)
                return;
            NotifyOfPropertyChange(() => SliderValue);
            var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(time, Editor);
            Editor.ScrollTo(tGrid);
        }

        public void OnStopButtonClicked()
        {
            if (AudioPlayer is null)
                return;

            Editor.UnlockAllUserInteraction();
            scrollAnimationClearFunc?.Invoke();
            FumenSoundPlayer.Stop();
            AudioPlayer.Stop();
        }

        public void OnSliderValueChanged()
        {
            Log.LogDebug($"seek by OnSliderValueChanged()");

            if (scrollAnimationClearFunc is null)
                InitPreviewActions();
            var seekTo = TimeSpan.FromMilliseconds(SliderValue);
            AudioPlayer.Seek(seekTo, true);
            FumenSoundPlayer.Seek(seekTo, true);

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
            if (!AudioPlayer.IsAvaliable)
            {
                Log.LogWarn($"音频还没准备好!");
                return;
            }
            if (AudioPlayer.IsPlaying)
            {
                scrollAnimationClearFunc?.Invoke();
                FumenSoundPlayer.Pause();
                AudioPlayer.Pause();
            }
            else
            {
                if (scrollAnimationClearFunc is null)
                    InitPreviewActions();
                Log.LogDebug($"seek by RequestPlayOrPause()");
                var tgrid = Editor.GetCurrentTGrid();
                var seekTo = TGridCalculator.ConvertTGridToAudioTime(tgrid, Editor);
                AudioPlayer.Seek(seekTo, false);
                FumenSoundPlayer.Seek(seekTo, false);
            }
        }

        public void OnSoundControlSwitchChanged(FrameworkElement sender)
        {
            var sc = 0;
            for (int i = 0; i < SoundControlLength; i++)
                sc = sc | (SoundControls[i] ? (1 << i) : 0);
            if (FumenSoundPlayer is IFumenSoundPlayer player)
                player.SoundControl = (SoundControl)sc;

            //Log.LogDebug($"Apply sound control:{(SoundControl)sc}");
            NotifyOfPropertyChange(() => SoundControls);
        }
    }
}
