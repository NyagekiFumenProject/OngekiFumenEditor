using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public interface IFumenSoundPlayer
    {
        SoundControl SoundControl { get; set; }
        Task Init(FumenVisualEditorViewModel editor, IAudioPlayer player);
        void Stop();
        void Play();
        void Pause();
        void Seek(TimeSpan msec,bool pause);
    }
}
