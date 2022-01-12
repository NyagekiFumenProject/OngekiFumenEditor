using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models
{
    public class EditorBindingModel : PropertyChangedBase
    {
        private IAudioPlayer audioPlayer = default;
        public IAudioPlayer AudioPlayer
        {
            get => audioPlayer;
            set => Set(ref audioPlayer, value);
        }


        private string audioName = default;
        public string AudioName
        {
            get => audioName;
            set => Set(ref audioName, value);
        }
    }
}
