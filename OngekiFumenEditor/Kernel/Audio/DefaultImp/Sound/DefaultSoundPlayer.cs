using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.DefaultImp.DefaultAudioManager;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp.Sound
{
    internal class DefaultSoundPlayer : ISoundPlayer
    {
        private DefaultAudioManager soundManager = default;
        private CachedSound cacheSound = default;

        public DefaultSoundPlayer(CachedSound cache, DefaultAudioManager manager)
        {
            soundManager = manager;
            cacheSound = cache;
        }

        public void Dispose()
        {

        }

        public void PlayLoop()
        {

        }

        public void PlayOnce()
        {
            soundManager.PlaySound(cacheSound);
        }

        public void StopLoop()
        {

        }
    }
}
