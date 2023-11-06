using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

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
				AudioPlayer = Editor?.AudioPlayer;
			}
		}

		private IAudioPlayer audioPlayer;
		public IAudioPlayer AudioPlayer
		{
			get => audioPlayer;
			private set
			{
				if (audioPlayer is not null)
					audioPlayer.OnPlaybackFinished -= OnPlaybackFinished;
				Set(ref audioPlayer, value);
				if (audioPlayer is not null)
					audioPlayer.OnPlaybackFinished += OnPlaybackFinished;

				PrepareWaveform(AudioPlayer);
				NotifyOfPropertyChange(() => IsAudioButtonEnabled);
			}
		}

		private void OnPlaybackFinished()
		{
			Dispatcher.CurrentDispatcher.Invoke(() =>
			{
				Log.LogInfo($"OnPlaybackFinished()~~");
				OnStopButtonClicked();
				if (AudioPlayer is not null)
				{
					var audioTime = AudioPlayer.Duration - TimeSpan.FromSeconds(1);
					Editor.ScrollTo(audioTime);
				}
			});
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
				SoundVolumes = sounds.Select(x => new SoundVolumeProxy(value, x)).Where(x => x.IsValid).ToArray();
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

		private async Task InitPreviewActions()
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
			//Editor.UnlockAllUserInteraction();
			scrollAnimationClearFunc?.Invoke();
			FumenSoundPlayer?.Stop();
			AudioPlayer?.Stop();
		}

		public void OnSliderValueStartChanged()
		{
			sliderDraggingValue = SliderValue;
			isSliderDragging = true;
			Log.LogDebug($"Begin drag, from : {SliderValue}");
		}

		public async void RequestPlayOrPause()
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
					await InitPreviewActions();
				var tgrid = Editor.GetCurrentTGrid();
				var seekTo = TGridCalculator.ConvertTGridToAudioTime(tgrid, Editor);
				Log.LogDebug($"seek to {tgrid}({seekTo})");
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

		public async void OnReloadSoundFiles()
		{
			if (AudioPlayer is null || FumenSoundPlayer is null)
			{
				MessageBox.Show("请先等待音频/音效初始化完成");
				return;
			}

			if (AudioPlayer.IsPlaying)
			{
				MessageBox.Show("请先暂停音频/谱面播放");
				return;
			}

			var result = await FumenSoundPlayer.ReloadSoundFiles();

			if (result)
			{
				MessageBox.Show("音效重新加载成功");
			}
		}
	}
}
