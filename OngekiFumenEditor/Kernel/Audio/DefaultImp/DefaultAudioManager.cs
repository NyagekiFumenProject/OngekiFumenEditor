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

        private readonly IWavePlayer soundOutputDevice;
        private readonly MixingSampleProvider soundMixer;
        private readonly VolumeSampleProvider soundVolumeWrapper;

        public float SoundVolume { get => soundVolumeWrapper.Volume; set => soundVolumeWrapper.Volume = value; }

        public DefaultAudioManager()
        {
            soundOutputDevice = new WasapiOut();
            soundMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
            soundMixer.ReadFully = true;
            soundVolumeWrapper = new VolumeSampleProvider(soundMixer);
            soundOutputDevice.Init(soundVolumeWrapper);
            soundOutputDevice.Play();
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == soundMixer.WaveFormat.Channels)
            {
                return input;
            }
            if (input.WaveFormat.Channels == 1 && soundMixer.WaveFormat.Channels == 2)
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
            soundMixer.AddMixerInput(ConvertToRightChannelCount(input));
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
            soundOutputDevice?.Dispose();
        }
    }
}
