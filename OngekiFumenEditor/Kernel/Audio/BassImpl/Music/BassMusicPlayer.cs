using Caliburn.Micro;
using ManagedBass;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Base;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl.Music
{
    internal class BassMusicPlayer : PropertyChangedBase, IAudioPlayer
    {
        private int audioHandle;
        private readonly int audioLatency;
        private FXVolumeParam volumeParam = new FXVolumeParam();
        private int volumeFXHandle;
        private bool isStoped;
        private byte[] samples;
        private SampleInfo info;
        private int syncHandle;

        public BassMusicPlayer(int audioHandle, int audioLatency, float[] sampleData, SampleInfo info)
        {
            this.audioHandle = audioHandle;
            this.audioLatency = audioLatency;
            var channelLength = Bass.ChannelGetLength(audioHandle);
            var durationSecond = Bass.ChannelBytes2Seconds(audioHandle, channelLength);

            Duration = TimeSpan.FromSeconds(durationSecond);

            volumeFXHandle = Bass.ChannelSetFX(audioHandle, (EffectType)9, 0);
            BassUtils.ReportError(nameof(Bass.ChannelSetFX));

            //init values
            Volume = 1;

            samples = new byte[sampleData.Length * sizeof(float)];
            MemoryMarshal.AsBytes<float>(sampleData).CopyTo(samples);

            this.info = info;

            syncHandle = Bass.ChannelSetSync(audioHandle, SyncFlags.End, 0, OnPlaybackStopped);
        }

        public TimeSpan CurrentTime => TimeSpan.FromSeconds(Math.Max(0, Bass.ChannelBytes2Seconds(audioHandle, Bass.ChannelGetPosition(audioHandle)) - audioLatency));

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
                NotifyOfPropertyChange(() => Volume);
            }
        }

        public TimeSpan Duration { get; init; }

        public bool IsPlaying
        {
            get
            {
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

            Bass.ChannelRemoveSync(audioHandle, syncHandle);
            Bass.ChannelRemoveFX(audioHandle, volumeFXHandle);
            Bass.StreamFree(audioHandle);
            audioHandle = 0;
            volumeFXHandle = 0;

        }

        public Task<SampleData> GetSamplesAsync()
        {
            var sampleData = new SampleData(samples, info);
            return Task.FromResult(sampleData);
        }

        private void OnPlaybackStopped(int Handle, int Channel, int Data, IntPtr User)
        {
            Bass.ChannelPlay(audioHandle, true);
            Bass.ChannelPause(audioHandle);

            var len = Bass.ChannelSeconds2Bytes(audioHandle, (Duration - TimeSpan.FromSeconds(1)).TotalSeconds);
            Bass.ChannelSetPosition(audioHandle, len, PositionFlags.Bytes);
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
