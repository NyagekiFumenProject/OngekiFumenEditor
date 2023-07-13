﻿using Caliburn.Micro;
using ControlzEx.Standard;
using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Audio.HCA;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Kernel.Audio.DefaultImp.Music;
using OngekiFumenEditor.Kernel.Audio.DefaultImp.Sound;
using OngekiFumenEditor.Kernel.Audio.NAudioImpl;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using static OngekiFumenEditor.Kernel.Audio.IAudioManager;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IAudioManager))]
    public class NAudioManager : IAudioManager
    {
        private HashSet<WeakReference<IAudioPlayer>> ownAudioPlayerRefs = new();

        private readonly IWavePlayer soundOutputDevice;
        private readonly MixingSampleProvider soundMixer;
        private readonly VolumeSampleProvider soundVolumeWrapper;

        public float SoundVolume { get => soundVolumeWrapper.Volume; set => soundVolumeWrapper.Volume = value; }

        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } = new[] {
            (".mp3","音频文件"),
            (".wav","音频文件"),
            (".acb","Criware音频文件"),
        };

        public NAudioManager()
        {
            soundOutputDevice = new WasapiOut(AudioClientShareMode.Shared, 0);
            soundMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            soundMixer.ReadFully = true;
            soundMixer.MixerInputEnded += SoundMixer_MixerInputEnded;
            soundVolumeWrapper = new VolumeSampleProvider(soundMixer);
            soundOutputDevice.Init(soundVolumeWrapper);
            soundOutputDevice.Play();

            Log.LogInfo($"Audio implement will use {GetType()}");
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
#if DEBUG
            if (input.WaveFormat.Channels == soundMixer.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && soundMixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }
            throw new NotImplementedException("Not yet implemented this channel count conversion");
#else
            return input;
#endif
        }

        public void PlaySound(CachedSound sound, float volume)
        {
            AddMixerInput(ConvertToRightChannelCount(new VolumeSampleProvider(new CachedSoundSampleProvider(sound))
            {
                Volume = volume
            }));
        }

        public void AddMixerInput(ISampleProvider input)
        {
            soundMixer.AddMixerInput(input);
        }

        public void RemoveMixerInput(ISampleProvider input)
        {
            soundMixer.RemoveMixerInput(input);
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            if (filePath.EndsWith(".acb"))
            {
                filePath = await AcbConverter.ConvertAcbFileToWavFile(filePath);
                if (filePath is null)
                    return null;
            }

            var player = new DefaultMusicPlayer();
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

            return Task.FromResult<ISoundPlayer>(new DefaultSoundPlayer(cached, this));
        }

        private CachedSound ResampleCacheSound(CachedSound cache)
        {
            var resampler = new WdlResamplingSampleProvider(new CachedSoundWrappedSampleProvider(cache), 48000);
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
            soundOutputDevice?.Dispose();
        }

        public ILoopHandle PlayLoopSound(CachedSound sound, float volume)
        {
            var provider = new VolumeSampleProvider(new LoopableProvider(ConvertToRightChannelCount(new CachedSoundSampleProvider(sound))));
            var handle = new NAudioLoopHandle(provider);
            handle.Volume = volume;

            //add to mixer
            AddMixerInput(provider);

            return handle;
        }

        public void StopLoopSound(ILoopHandle h)
        {
            if (h is not NAudioLoopHandle handle)
                return;

            RemoveMixerInput(handle.Provider);
        }

        private void SoundMixer_MixerInputEnded(object sender, SampleProviderEventArgs e)
        {

        }
    }
}