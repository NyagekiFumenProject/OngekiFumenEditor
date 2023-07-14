using Caliburn.Micro;
using ManagedBass;
using ManagedBass.Mix;
using Microsoft.VisualBasic.Devices;
using NAudio.Mixer;
using OngekiFumenEditor.Kernel.Audio.BassImpl.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl.Sound
{
    internal class BassSoundPlayer : PropertyChangedBase, ISoundPlayer
    {
        private record CacheHandle(int AudioHandle, int FxHandle);

        ConcurrentBag<CacheHandle> registerdSoundHandles = new();
        ConcurrentBag<CacheHandle> idleSoundHandles = new();

        private int soundMixer;
        private readonly byte[] buffer;
        private FXVolumeParam volumeParam = new FXVolumeParam();

        public BassSoundPlayer(int soundMixer, byte[] buffer)
        {
            this.soundMixer = soundMixer;
            this.buffer = buffer;
            InitSoundCache();

            //init values
            Volume = 1;
        }

        private CacheHandle GenerateNewCacheHandle()
        {
            var soundHandle = Bass.CreateStream(buffer, 0, buffer.Length, BassFlags.Default);
            BassUtils.ReportError(nameof(Bass.CreateStream));
            Bass.ChannelSetAttribute(soundHandle, ChannelAttribute.Buffer, 5);
            BassUtils.ReportError(nameof(Bass.ChannelSetAttribute));
            var volumeFXHandle = Bass.ChannelSetFX(soundHandle, (EffectType)9, 0);
            BassUtils.ReportError(nameof(Bass.ChannelSetFX));

            var handle = new CacheHandle(soundHandle, volumeFXHandle);
            registerdSoundHandles.Add(handle);
            return handle;
        }

        private void InitSoundCache()
        {
            for (int i = 0; i < 10; i++)
            {
                var handle = GenerateNewCacheHandle();
                idleSoundHandles.Add(handle);
            }
        }

        private CacheHandle GetPlayableSoundHandle()
        {
            if (!idleSoundHandles.TryTake(out var handle))
                handle = GenerateNewCacheHandle();

            return handle;
        }

        private void ReturnUsedSoundHandle(CacheHandle handle)
        {
            idleSoundHandles.Add(handle);
        }

        private void ClearSoundCache()
        {
            foreach (var cacheHandle in registerdSoundHandles)
            {
                Bass.ChannelRemoveFX(cacheHandle.AudioHandle,cacheHandle.FxHandle);
                Bass.StreamFree(cacheHandle.AudioHandle);
            }
        }

        public float Volume
        {
            get
            {
                //Bass.FXGetParameters(volumeFXHandle, volumeParam);
                return volumeParam.fCurrent;
            }

            set
            {

                volumeParam.fCurrent = value;
                volumeParam.fTarget = value;
                volumeParam.fTime = 0;
                //Bass.FXSetParameters(volumeFXHandle, volumeParam);
                NotifyOfPropertyChange(() => Volume);
            }
        }

        public bool IsAvaliable { get; } = true;

        public TimeSpan Duration => throw new NotImplementedException();

        public void Dispose()
        {
            if (!IsAvaliable)
                return;

            ClearSoundCache();

            soundMixer = default;
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
            var handle = GetPlayableSoundHandle();
            var syncHandle = 0;
            syncHandle = Bass.ChannelSetSync(handle.AudioHandle, SyncFlags.End, 0, (int Handle, int Channel, int Data, IntPtr User) =>
            {
                Bass.ChannelRemoveSync(handle.AudioHandle, syncHandle);
                Bass.ChannelSetPosition(handle.AudioHandle, 0);
                ReturnUsedSoundHandle(handle);

            }, default);

            BassMix.MixerAddChannel(soundMixer, handle.AudioHandle, BassFlags.Default);

            Bass.FXSetParameters(handle.FxHandle, volumeParam);
            Bass.ChannelPlay(handle.AudioHandle);
        }

        public void StopLoop()
        {
            if (!IsAvaliable)
                return;
        }

        public void PlayLoop(int loopId, TimeSpan init)
        {
            throw new NotImplementedException();
        }

        public void StopLoop(int loopId)
        {
            throw new NotImplementedException();
        }
    }
}
