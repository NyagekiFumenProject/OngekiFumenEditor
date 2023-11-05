using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.DefaultImp.Music;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
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

		private readonly IWavePlayer audioOutputDevice;

		private readonly MixingSampleProvider audioMixer;
		private readonly MixingSampleProvider soundMixer;
		private readonly MixingSampleProvider musicMixer;

		private readonly VolumeSampleProvider soundVolumeWrapper;
		private readonly VolumeSampleProvider musicVolumeWrapper;

		public float SoundVolume { get => soundVolumeWrapper.Volume; set => soundVolumeWrapper.Volume = value; }
		public float MusicVolume { get => musicVolumeWrapper.Volume; set => musicVolumeWrapper.Volume = value; }

		public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
			(".mp3","音频文件"),
			(".wav","音频文件"),
			(".acb","Criware音频文件"),
		};

		public NAudioManager()
		{
			var audioOutputType = (AudioOutputType)Properties.AudioSetting.Default.AudioOutputType;
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

			audioMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
			audioMixer.ReadFully = true;
			audioOutputDevice.Init(audioMixer);
			audioOutputDevice.Play();

			//setup sound
			soundMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
			soundMixer.ReadFully = true;
			soundVolumeWrapper = new VolumeSampleProvider(soundMixer);
			audioMixer.AddMixerInput(soundVolumeWrapper);

			//setup sound
			musicMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
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
			await player.Load(filePath);
			return player;
		}

		public Task<ISoundPlayer> LoadSoundAsync(string filePath)
		{
			var cached = new CachedSound(filePath);
			if (cached.WaveFormat.SampleRate != 48000)
			{
				Log.LogWarn($"Resample sound audio file from {cached.WaveFormat.SampleRate} to 48000 : {filePath}");
				cached = ResampleCacheSound(cached);
			}

			if (cached.WaveFormat.Channels == 1 && audioMixer.WaveFormat.Channels == 2)
			{
				Log.LogWarn($"Extend channel from Mono to Stereo : {filePath}");
				cached = MonoToStereoSound(cached);
			}

			return Task.FromResult<ISoundPlayer>(new NAudioSoundPlayer(cached, this));
		}

		private CachedSound MonoToStereoSound(CachedSound cache)
		{
			var converter = new MonoToStereoSampleProvider(cache.CreateSampleProvider());
			var buffer = ArrayPool<float>.Shared.Rent(1024_000);
			var outFormat = converter.WaveFormat;
			var list = new List<(float[], int)>();

			while (true)
			{
				var read = converter.Read(buffer, 0, buffer.Length);
				if (read == 0)
					break;

				var b = ArrayPool<float>.Shared.Rent(read);
				buffer.AsSpan()[..read].CopyTo(b);
				list.Add((b, read));
			}

			var totalLength = list.Select(x => x.Item2).Sum();
			var newBuf = new float[totalLength];

			var r = 0;
			foreach ((var b, var read) in list)
			{
				b.AsSpan()[..read].CopyTo(newBuf.AsSpan().Slice(r, read));
				r += read;
				ArrayPool<float>.Shared.Return(b);
			}

			ArrayPool<float>.Shared.Return(buffer);
			return new CachedSound(newBuf, outFormat);
		}

		private CachedSound ResampleCacheSound(CachedSound cache)
		{
			var resampler = new WdlResamplingSampleProvider(cache.CreateSampleProvider(), 48000);
			var buffer = ArrayPool<float>.Shared.Rent(1024_000);
			var outFormat = resampler.WaveFormat;
			var list = new List<(float[], int)>();

			while (true)
			{
				var read = resampler.Read(buffer, 0, buffer.Length);
				if (read == 0)
					break;

				var b = ArrayPool<float>.Shared.Rent(read);
				buffer.AsSpan()[..read].CopyTo(b);
				list.Add((b, read));
			}

			var totalLength = list.Select(x => x.Item2).Sum();
			var newBuf = new float[totalLength];

			var r = 0;
			foreach ((var b, var read) in list)
			{
				b.AsSpan()[..read].CopyTo(newBuf.AsSpan().Slice(r, read));
				r += read;
				ArrayPool<float>.Shared.Return(b);
			}

			ArrayPool<float>.Shared.Return(buffer);
			return new CachedSound(newBuf, outFormat);
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
