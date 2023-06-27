using Caliburn.Micro;
using ManagedBass;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl.Music
{
    internal class BassMusicPlayer : PropertyChangedBase, IAudioPlayer
    {
        private int audioHandle;
        private FXVolumeParam volumeParam = new FXVolumeParam()
        {
            volume = 1,
            lChannel = FXChannelFlags.All
        };
        private int volumeFXHandle;
        private bool isStoped;

        public BassMusicPlayer(int audioHandle)
        {
            this.audioHandle = audioHandle;

            var channelLength = Bass.ChannelGetLength(audioHandle);
            var durationSecond = Bass.ChannelBytes2Seconds(audioHandle,channelLength);

            Duration = TimeSpan.FromSeconds(durationSecond);

            volumeFXHandle = Bass.ChannelSetFX(audioHandle, EffectType.Volume, 0);

            //init values
            Volume = 1;
        }

        public TimeSpan CurrentTime => TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(audioHandle,Bass.ChannelGetPosition(audioHandle)));

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

        public TimeSpan Duration { get; init; }

        public bool IsPlaying
        {
            get {
                var state = Bass.ChannelIsActive(audioHandle);
                switch (state)
                {
                    case PlaybackState.Playing:
                        return true;
                    case PlaybackState.Stopped:
                    case PlaybackState.Stalled:
                    case PlaybackState.Paused:
                    default:
                        return false;
                }

            }
        }

        public bool IsAvaliable => audioHandle != 0;

        public void Dispose()
        {
            if (!IsAvaliable)
                return;

            Bass.ChannelRemoveFX(audioHandle, volumeFXHandle);
            Bass.StreamFree(audioHandle);
            audioHandle = 0;
        }

        public async Task<SampleData> GetSamplesAsync()
        {
            return null;
        }

        public void Pause()
        {
            if (!IsPlaying)
                return;
            if (!IsAvaliable)
                return;

            Bass.ChannelPause(audioHandle);
            isStoped = false;
        }

        public void Play()
        {
            if (IsPlaying)
                return;
            if (!IsAvaliable)
                return;

            Bass.ChannelPlay(audioHandle, isStoped);
            isStoped = false;
        }

        public void Seek(TimeSpan TimeSpan, bool pause)
        {
            if (!IsAvaliable)
                return;
            if (pause)
                Pause();
            var len = Bass.ChannelSeconds2Bytes(audioHandle, TimeSpan.TotalSeconds);
            Bass.ChannelSetPosition(audioHandle, len, PositionFlags.Bytes);
            if (!pause)
                Play();
        }

        public void Stop()
        {
            if (!IsAvaliable)
                return;
            Bass.ChannelStop(audioHandle);
            isStoped = true;
        }
    }
}
