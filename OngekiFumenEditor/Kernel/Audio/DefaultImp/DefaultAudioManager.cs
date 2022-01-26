using Caliburn.Micro;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OngekiFumenEditor.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IAudioManager))]
    public class DefaultAudioManager : IAudioManager
    {
        private HashSet<IAudioPlayer> ownAudioPlayers = new();

        public class CachedSound
        {
            public float[] AudioData { get; private set; }
            public WaveFormat WaveFormat { get; private set; }

            public CachedSound(string audioFileName)
            {
                using var audioFileReader = new AudioFileReader(audioFileName);

                WaveFormat = audioFileReader.WaveFormat;
                var wholeFile = new List<float>((int)(audioFileReader.Length / 4));

                var readLen = audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels;
                var readBuffer = ArrayPool<float>.Shared.Rent(readLen);

                int samplesRead;
                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readLen)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }

                AudioData = wholeFile.ToArray();
                ArrayPool<float>.Shared.Return(readBuffer);
            }
        }

        private class CachedSoundSampleProvider : ISampleProvider
        {
            private readonly CachedSound cachedSound;
            private long position;

            public WaveFormat WaveFormat => cachedSound.WaveFormat;

            public CachedSoundSampleProvider(CachedSound cachedSound)
            {
                this.cachedSound = cachedSound;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                var availableSamples = cachedSound.AudioData.Length - position;
                var samplesToCopy = Math.Min(availableSamples, count);
                Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
                position += samplesToCopy;
                return (int)samplesToCopy;
            }
        }

        private readonly IWavePlayer outputDevice;
        private readonly MixingSampleProvider mixer;

        public DefaultAudioManager()
        {
            outputDevice = new WasapiOut();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            mixer.ReadFully = true;
            outputDevice.Init(mixer);
            outputDevice.Play();
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }
            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        public void PlaySound(CachedSound sound)
        {
            AddMixerInput(new CachedSoundSampleProvider(sound));
        }

        public void AddMixerInput(ISampleProvider input)
        {
            mixer.AddMixerInput(ConvertToRightChannelCount(input));
        }

        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            var player = new DefaultMusicPlayer();
            ownAudioPlayers.Add(player);

            await player.Load(filePath);

            return player;
        }

        public Task<ISoundPlayer> LoadSoundAsync(string filePath)
        {
            var cached = new CachedSound(filePath);
            return Task.FromResult<ISoundPlayer>(new DefaultSoundPlayer(cached, this));
        }

        public void Dispose()
        {
            Log.LogDebug("call DefaultAudioManager.Dispose()");
            foreach (var player in ownAudioPlayers)
                player?.Dispose();
            ownAudioPlayers.Clear();
            outputDevice?.Dispose();
        }
    }
}
