using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.DefaultImp.NAudioManager;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Sound
{
    internal class DefaultSoundPlayer : ISoundPlayer
    {
        public TimeSpan Duration => cacheSound.Duration;

        private NAudioManager soundManager = default;
        private CachedSound cacheSound = default;
        private Dictionary<int, ILoopHandle> loopMap = new Dictionary<int, ILoopHandle>();

        public DefaultSoundPlayer(CachedSound cache, NAudioManager manager)
        {
            soundManager = manager;
            cacheSound = cache;
        }

        public float Volume { get; set; } = 1;

        public void Dispose()
        {

        }

        public void PlayOnce()
        {
            soundManager.PlaySound(cacheSound, Volume);
        }

        public void PlayLoop(int loopId)
        {
            if (!loopMap.ContainsKey(loopId))
            {
                var handle = soundManager.PlayLoopSound(cacheSound, Volume);
                loopMap[loopId] = handle;
            }
            else
            {
                //todo warn
            }
        }

        public void StopLoop(int loopId)
        {
            if (loopMap.TryGetValue(loopId, out var handle))
            {
                soundManager.StopLoopSound(handle);
                loopMap.Remove(loopId);
            }
            else
            {
                //todo warn
            }
        }
    }
}
