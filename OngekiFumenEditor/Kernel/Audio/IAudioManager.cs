using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public interface IAudioManager : IDisposable
    {
        Task<ISoundPlayer> LoadSoundAsync(string filePath);
        Task<IAudioPlayer> LoadAudioAsync(string filePath);
    }
}
