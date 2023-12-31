using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.DefaultImp.Music;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Utils;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl
{
	[Export(typeof(IAudioManager))]
	public class NAudioManager : IAudioManager
	{
		private HashSet<WeakReference<IAudioPlayer>> ownAudioPlayerRefs = new();
		private int targetSampleRate;
		private readonly IWavePlayer audioOutputDevice;

		private readonly MixingSampleProvider audioMixer;
		private readonly MixingSampleProvider soundMixer;
		private readonly MixingSampleProvider musicMixer;

		private readonly VolumeSampleProvider soundVolumeWrapper;
		private readonly VolumeSampleProvider musicVolumeWrapper;

		public float SoundVolume { get => soundVolumeWrapper.Volume; set => soundVolumeWrapper.Volume = value; }
		public float MusicVolume { get => musicVolumeWrapper.Volume; set => musicVolumeWrapper.Volume = value; }

		public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
			(".mp3","Audio File"),
			(".wav","Audio File"),
			(".acb","Criware Audio File"),
		};

		public NAudioManager()
		{
			var audioOutputType = (AudioOutputType)Properties.AudioSetting.Default.AudioOutputType;
			targetSampleRate = Properties.AudioSetting.Default.AudioSampleRate;
			Log.LogDebug($"targetSampleRate: {targetSampleRate}");
			Log.LogDebug($"audioOutputType: {audioOutputType}");

			try
			{
				audioOutputDevice = audioOutputType switch
				{
					AudioOutputType.Asio => new AsioOut() { AutoStop = false },
					AudioOutputType.Wasapi => new WasapiOut(AudioClientShareMode.Shared, 0),
					AudioOutputType.WaveOut or _ => new WaveOut() { DesiredLatency = 100 },
				};
			}
			catch (Exception e)
			{
				Log.LogError($"Can't create audio output device:{audioOutputType}", e);
				throw;
			}
			Log.LogDebug($"audioOutputDevice: {audioOutputDevice}");

			var format = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, 2);

			audioMixer = new MixingSampleProvider(format);
			audioMixer.ReadFully = true;
			audioOutputDevice.Init(audioMixer);
			audioOutputDevice.Play();

			//setup sound
			soundMixer = new MixingSampleProvider(format);
			soundMixer.ReadFully = true;
			soundVolumeWrapper = new VolumeSampleProvider(soundMixer);
			audioMixer.AddMixerInput(soundVolumeWrapper);

			//setup sound
			musicMixer = new MixingSampleProvider(format);
			musicMixer.ReadFully = true;
			musicVolumeWrapper = new VolumeSampleProvider(musicMixer);
			audioMixer.AddMixerInput(musicVolumeWrapper);

			Log.LogInfo($"Audio implement will use {GetType()}");
		}

		public void PlaySound(CachedSound sound, float volume, TimeSpan init)
		{
			ISampleProvider provider = new VolumeSampleProvider(new CachedSoundSampleProvider(sound))
			{
				Volume = volume
			};
			if (init.TotalMilliseconds != 0)
			{
				provider = new OffsetSampleProvider(provider)
				{
					SkipOver = init
				};
			}

			AddSoundMixerInput(provider);
		}

		public void AddSoundMixerInput(ISampleProvider input)
		{
			soundMixer.AddMixerInput(input);
		}

		public void RemoveSoundMixerInput(ISampleProvider input)
		{
			soundMixer.RemoveMixerInput(input);
		}

		public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return null;

			if (filePath.EndsWith(".acb"))
			{
				filePath = await AcbConverter.ConvertAcbFileToWavFile(filePath);
				if (filePath is null)
					return null;
			}

			var player = new DefaultMusicPlayer(musicMixer);
			ownAudioPlayerRefs.Add(new WeakReference<IAudioPlayer>(player));
			await player.Load(filePath, targetSampleRate);
			return player;
		}

		public async Task<ISoundPlayer> LoadSoundAsync(string filePath)
		{
			using var audioFileReader = new AudioFileReader(filePath);
			Log.LogInfo($"Load sound file: {filePath}");

			var provider = await AudioCompatibilizer.CheckCompatible(audioFileReader, targetSampleRate);

			return new NAudioSoundPlayer(new CachedSound(provider), this);
		}

		public void Dispose()
		{
			Log.LogDebug("call DefaultAudioManager.Dispose()");
			foreach (var weakRef in ownAudioPlayerRefs)
			{
				if (weakRef.TryGetTarget(out var player))
					player?.Dispose();
			}
			ownAudioPlayerRefs.Clear();
			audioOutputDevice?.Dispose();
		}

		public ILoopHandle PlayLoopSound(CachedSound sound, float volume, TimeSpan init)
		{
			ISampleProvider provider = new LoopableProvider(new CachedSoundSampleProvider(sound));

			if (init.TotalMilliseconds != 0)
			{
				provider = new OffsetSampleProvider(provider)
				{
					SkipOver = init
				};
			}

			var handle = new NAudioLoopHandle(new VolumeSampleProvider(provider));
			handle.Volume = volume;

			//add to mixer
			AddSoundMixerInput(handle.Provider);

			//Log.LogDebug($"handle hashcode = {handle.GetHashCode()}");
			return handle;
		}

		public void StopLoopSound(ILoopHandle h)
		{
			if (h is not NAudioLoopHandle handle)
				return;

			//Log.LogDebug($"handle hashcode = {handle.GetHashCode()}");
			RemoveSoundMixerInput(handle.Provider);
		}
	}
}
