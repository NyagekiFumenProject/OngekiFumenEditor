using OngekiFumenEditor.Kernel.Audio.NAudioImpl.Sound;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public partial interface IAudioManager : IDisposable
    {
        float SoundVolume { get; set; }

        Task<ISoundPlayer> LoadSoundAsync(string filePath);
        Task<IAudioPlayer> LoadAudioAsync(string filePath);

        IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; }
    }
}
