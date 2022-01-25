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
        Task Init(FumenVisualEditorViewModel editor, IAudioPlayer player);
        void Stop();
        void Play();
        void Pause();
        void Seek(float msec);
    }
}
