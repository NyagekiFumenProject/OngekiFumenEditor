using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultImp
{
    [Export(typeof(IAudioManager))]
    public class DefaultAudioManager : IAudioManager
    {
        public async Task<IAudioPlayer> LoadAudioAsync(string filePath)
        {
            var player = new DefaultMusicPlayer();

            await player.Load(filePath);

            return player;
        }
    }
}
