using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.DefaultCommonImpl.Sound
{
    public class DurationSoundEvent : SoundEvent
    {
        public int LoopId { get; set; }
        //public TGrid EndTGrid { get; set; }
        public TimeSpan EndTime { get; set; }

        public override string ToString() => $"{base.ToString()} {LoopId} {EndTime}";
    }
}
