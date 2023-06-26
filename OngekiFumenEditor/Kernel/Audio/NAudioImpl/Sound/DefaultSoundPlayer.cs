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
        private NAudioManager soundManager = default;
        private CachedSound cacheSound = default;

        public DefaultSoundPlayer(CachedSound cache, NAudioManager manager)
        {
            soundManager = manager;
            cacheSound = cache;
        }

        public float Volume { get; set; } = 1;

        public void Dispose()
        {

        }

        public void PlayLoop()
        {

        }

        public void PlayOnce()
        {
            soundManager.PlaySound(cacheSound, Volume);
        }

        public void StopLoop()
        {

        }
    }
}
