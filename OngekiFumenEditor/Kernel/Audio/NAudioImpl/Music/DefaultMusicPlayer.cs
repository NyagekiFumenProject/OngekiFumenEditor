using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Music
{
    internal class DefaultMusicPlayer : PropertyChangedBase, IAudioPlayer, ISchedulable
	{
		private FinishedListenerProvider finishProvider;

		private VolumeSampleProvider currentVolumeProvider;
		private TimeSpan baseOffset = TimeSpan.FromMilliseconds(0);
		private Stopwatch sw = new();
		private TimeSpan pauseTime;
		private float volume = 1;
		private bool isAvaliable;
		private byte[] samples;
		private BufferWaveStream audioFileReader;
		private MixingSampleProvider musicMixer;

		public event IAudioPlayer.OnPlaybackFinishedFunc OnPlaybackFinished;

		public TimeSpan Duration => duration;

		public TimeSpan CurrentTime { get => GetTime(); }

		public float Speed { get => 1; set { } }

		private bool isPlaying;
		private TimeSpan duration;

		public bool IsPlaying
		{
			get => isPlaying;
			set => Set(ref isPlaying, value);
		}

		public float Volume
		{
			get => volume;
			set
			{
				volume = value;
				if (currentVolumeProvider is not null)
					currentVolumeProvider.Volume = volume;
			}
		}

		public string SchedulerName => $"DefaultMusicPlayer Playing Updater";

		public TimeSpan ScheduleCallLoopInterval => TimeSpan.FromMilliseconds(1000.0 / 60);

		public bool IsAvaliable
		{
			get => isAvaliable;
			set
			{
				Set(ref isAvaliable, value);
			}
		}

		public DefaultMusicPlayer(MixingSampleProvider soundMixer)
		{
			this.musicMixer = soundMixer;
		}

		private void Provider_OnReturnEmptySamples()
		{
			finishProvider.StopListen();
			OnPlaybackFinished?.Invoke();
		}

		public async Task Load(string audio_file, int targetSampleRate)
		{
			//release resource before loading new one.
			Dispose();

			try
			{
				Log.LogInfo($"Load audio file: {audio_file}");
				var rawStream = new AudioFileReader(audio_file);
				duration = rawStream.TotalTime;
				var processedProvider = await AudioCompatibilizer.CheckCompatible(rawStream, targetSampleRate);

				samples = processedProvider.ToWaveProvider().ToArray();

				audioFileReader = new BufferWaveStream(samples, processedProvider.WaveFormat);
				audioFileReader.Seek(0, SeekOrigin.Begin);
				currentVolumeProvider = new(audioFileReader);
				finishProvider = new(currentVolumeProvider);
				finishProvider.StartListen();
				finishProvider.OnReturnEmptySamples += Provider_OnReturnEmptySamples;

				NotifyOfPropertyChange(() => Duration);
				IsAvaliable = true;
			}
			catch (Exception e)
			{
				Log.LogError($"Load audio file ({audio_file}) failed : {e.Message}");
				Dispose();
			}
		}

		public void Seek(TimeSpan time, bool pause)
		{
			time = MathUtils.Max(TimeSpan.FromMilliseconds(0), MathUtils.Min(time, Duration));

			audioFileReader.Seek((long)(audioFileReader.WaveFormat.AverageBytesPerSecond * time.TotalSeconds), SeekOrigin.Begin);
			baseOffset = time;

			finishProvider.StartListen();

			UpdatePropsManually();
			if (!pause)
				Play();
		}

		public async void Play()
		{
			IsPlaying = true;
			sw.Restart();
			musicMixer.AddMixerInput(finishProvider);
			await IoC.Get<ISchedulerManager>().AddScheduler(this);
		}

		private TimeSpan GetTime()
		{
			if (!IsPlaying)
				return pauseTime;
			var offset = TimeSpan.FromTicks(sw.ElapsedTicks);
			var actualTime = offset + baseOffset;
			return actualTime;
		}

		public async void Stop()
		{
			IsPlaying = false;
			musicMixer.RemoveMixerInput(finishProvider);
			await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
			Seek(TimeSpan.FromMilliseconds(0), true);
		}

		public async void Pause()
		{
			pauseTime = GetTime();
			IsPlaying = false;
			musicMixer.RemoveMixerInput(finishProvider);
			UpdatePropsManually();
			await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
		}

		private void CleanCurrentOut()
		{
			musicMixer.RemoveMixerInput(finishProvider);
			UpdatePropsManually();
		}

		public async void Dispose()
		{
			CleanCurrentOut();

			audioFileReader?.Dispose();
			audioFileReader = null;
			IsAvaliable = false;
			IsPlaying = false;

			await IoC.Get<ISchedulerManager>().RemoveScheduler(this);
		}

		public void OnSchedulerTerm()
		{

		}

		public Task OnScheduleCall(CancellationToken cancellationToken)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				UpdatePropsManually();
			}
			return Task.CompletedTask;
		}

		private void UpdatePropsManually()
		{
			if (!IsAvaliable)
				return;

			NotifyOfPropertyChange(() => CurrentTime);
			NotifyOfPropertyChange(() => Volume);
			NotifyOfPropertyChange(() => Speed);
			NotifyOfPropertyChange(() => IsPlaying);
		}

		public Task<SampleData> GetSamplesAsync()
		{
			if (!IsAvaliable)
				return Task.FromResult<SampleData>(default);

			var subBuffer = samples.AsMemory();
			var sampleData = new SampleData(subBuffer, ConvertToSampleInfo(audioFileReader.WaveFormat));

			return Task.FromResult(sampleData);
		}

		public static SampleInfo ConvertToSampleInfo(WaveFormat waveFormat)
		{
			var sampleInfo = new SampleInfo();

			sampleInfo.SampleRate = waveFormat.SampleRate;
			sampleInfo.Channels = waveFormat.Channels;
			sampleInfo.BitsPerSample = waveFormat.BitsPerSample;

			return sampleInfo;
		}
	}
}
