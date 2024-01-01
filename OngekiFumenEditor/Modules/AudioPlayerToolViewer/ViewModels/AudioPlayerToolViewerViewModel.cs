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

		private IFumenSoundPlayer fumenSoundPlayer = default;
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

		public bool IsAudioButtonEnabled => AudioPlayer is not null;

		public AudioPlayerToolViewerViewModel()
		{
			DisplayName = Resources.AudioPlayerToolViewer;
			FumenSoundPlayer = IoC.Get<IFumenSoundPlayer>();
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;

			CompositionTarget.Rendering += CompositionTarget_Rendering;
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

		private void CompositionTarget_Rendering(object sender, EventArgs e)
		{
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
			var tGrid = TGridCalculator.ConvertAudioTimeToTGrid(time, Editor);
			Editor.ScrollTo(tGrid);
		}

		public void OnStopButtonClicked()
		{
			//Editor.UnlockAllUserInteraction();
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
				OnStopButtonClicked();
			}
			else
			{
				await FumenSoundPlayer.Prepare(Editor, AudioPlayer);
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
			var length = Enum.GetValues<SoundControl>().Length;
			for (int i = 0; i < length; i++)
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
				MessageBox.Show(Resources.WaitForAudioAndFumenLoaded);
				return;
			}

			if (AudioPlayer.IsPlaying)
			{
				MessageBox.Show(Resources.PauseAudioAndFumen);
				return;
			}

			var result = await FumenSoundPlayer.ReloadSoundFiles();

			if (result)
			{
				MessageBox.Show(Resources.SoundLoaded);
			}
		}

		public void Dispose()
		{
			CompositionTarget.Rendering -= CompositionTarget_Rendering;
		}
	}
}
