using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound
{
    internal class NAudioSoundPlayer : ISoundPlayer
    {
        public TimeSpan Duration => cacheSound.Duration;

        private NAudioManager soundManager = default;
        private CachedSound cacheSound = default;
        private readonly object loopLocker = new();
        private readonly Dictionary<int, ILoopHandle> loopMap = new();

        public NAudioSoundPlayer(CachedSound cache, NAudioManager manager)
        {
            soundManager = manager;
            cacheSound = cache;
        }

        private float volume = 1;
        public float Volume
        {
            get => volume;
            set
            {
                lock (loopLocker)
                {
                    volume = value;
                    foreach (var handle in loopMap.Values)
                        handle.Volume = value;
                }
            }
        }

        public void Dispose()
        {

        }

        public void PlayOnce()
        {
            soundManager.PlaySound(cacheSound, Volume, TimeSpan.Zero);
        }

        public void PlayLoop(int loopId, TimeSpan init)
        {
            lock (loopLocker)
            {
                if (!loopMap.ContainsKey(loopId))
                {
                    var handle = soundManager.PlayLoopSound(cacheSound, volume, init);
                    loopMap[loopId] = handle;
                }
                else
                {
                    OngekiFumenEditor.Utils.Log.LogWarn($"Play loop sound ignored because loop id already exists: loopId={loopId}");
                }
            }
        }

        public void StopLoop(int loopId)
        {
            lock (loopLocker)
            {
                if (loopMap.TryGetValue(loopId, out var handle))
                {
                    soundManager.StopLoopSound(handle);
                    loopMap.Remove(loopId);
                }
                else
                {
                    OngekiFumenEditor.Utils.Log.LogWarn($"Stop loop sound ignored because loop id does not exist: loopId={loopId}");
                }
            }
        }
    }
}
