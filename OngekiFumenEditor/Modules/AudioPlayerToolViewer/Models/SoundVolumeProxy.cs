using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.DefaultImp.Sound.DefaultFumenSoundPlayer;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Models
{
    public class SoundVolumeProxy : PropertyChangedBase
    {
        private readonly IFumenSoundPlayer soundPlayer;
        private readonly Sound sound;

        public string Name => sound.ToString();

        public float Volume
        {
            get => soundPlayer.GetVolume(sound);
            set
            {
                soundPlayer.SetVolume(sound, value);
                NotifyOfPropertyChange(() => Volume);
            }
        }

        public SoundVolumeProxy(IFumenSoundPlayer soundPlayer, Sound sound)
        {
            this.soundPlayer = soundPlayer;
            this.sound = sound;
        }
    }
}
