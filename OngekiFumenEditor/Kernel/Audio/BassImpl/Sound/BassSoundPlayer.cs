using ManagedBass;
using ManagedBass.Mix;
using Microsoft.VisualBasic.Devices;
using NAudio.Mixer;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl.Sound
{
    internal class BassSoundPlayer : ISoundPlayer
    {
        private int soundHandle;
        private int soundMixer;
        private int volumeFXHandle;
        private FXVolumeParam volumeParam = new FXVolumeParam()
        {
            volume = 1,
            lChannel = FXChannelFlags.All
        };
        private bool added = false;

        public bool IsAvaliable => soundHandle != 0;

        public BassSoundPlayer(int soundHandle, int soundMixer)
        {
            this.soundHandle = soundHandle;
            this.soundMixer = soundMixer;

            volumeFXHandle = Bass.ChannelSetFX(soundHandle, EffectType.Volume, 0);
            //init values
            Volume = 1;
        }

        public float Volume
        {
            get
            {
                Bass.FXGetParameters(volumeFXHandle, volumeParam);
                return volumeParam.volume;
            }

            set
            {
                volumeParam.volume = value;
                Bass.FXSetParameters(volumeFXHandle, volumeParam);
            }
        }

        public void Dispose()
        {
            if (!IsAvaliable)
                return;

            MakeSureChannelRemoved();
            Bass.StreamFree(soundHandle);
            soundHandle = default;
            soundMixer = default;
        }

        private void MakeSureChannelRemoved()
        {
            if (added)
                BassMix.MixerRemoveChannel(soundHandle);
            added = false;  
        }

        public void PlayLoop()
        {
            if (!IsAvaliable)
                return;
        }

        public void PlayOnce()
        {
            if (!IsAvaliable)
                return;
            MakeSureChannelRemoved();
            Bass.ChannelSetPosition(soundHandle, 0);
            BassMix.MixerAddChannel(soundMixer, soundHandle, BassFlags.Default);
            Bass.ChannelPlay(soundHandle);
            added = true;
        }

        public void StopLoop()
        {
            if (!IsAvaliable)
                return;
        }
    }
}
