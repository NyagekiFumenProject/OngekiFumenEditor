using Caliburn.Micro;
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
    internal class BassSoundPlayer : PropertyChangedBase, ISoundPlayer
    {
        private int soundHandle;
        private int soundMixer;
        private int volumeFXHandle;
        private FXVolumeParam volumeParam = new FXVolumeParam();
        private bool added = false;

        public bool IsAvaliable => soundHandle != 0;

        public BassSoundPlayer(int soundHandle, int soundMixer)
        {
            this.soundHandle = soundHandle;
            this.soundMixer = soundMixer;

            volumeFXHandle = Bass.ChannelSetFX(soundHandle, (EffectType)9, 0);
            BassUtils.ReportError(nameof(Bass.ChannelSetFX));
            //init values
            Volume = 1;
        }

        public float Volume
        {
            get
            {
                Bass.FXGetParameters(volumeFXHandle, volumeParam);
                return volumeParam.fCurrent;
            }

            set
            {

                volumeParam.fCurrent = value;
                volumeParam.fTarget = value;
                volumeParam.fTime = 0;
                Bass.FXSetParameters(volumeFXHandle, volumeParam);
                NotifyOfPropertyChange(()=>Volume);
            }
        }

        public void Dispose()
        {
            if (!IsAvaliable)
                return;

            MakeSureChannelRemoved();

            Bass.ChannelRemoveFX(soundHandle,volumeFXHandle);
            Bass.StreamFree(soundHandle);

            soundHandle = default;
            soundMixer = default;
            volumeFXHandle= default;
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
