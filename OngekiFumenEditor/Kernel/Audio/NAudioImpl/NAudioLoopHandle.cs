using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.IAudioManager;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl
{
    public class NAudioLoopHandle : ILoopHandle
    {
        public float Volume
        {
            get => sampleProvider.Volume;
            set => sampleProvider.Volume = value;
        }

        public VolumeSampleProvider Provider => sampleProvider;

        private readonly VolumeSampleProvider sampleProvider;

        public NAudioLoopHandle(VolumeSampleProvider sampleProvider)
        {
            this.sampleProvider = sampleProvider;
        }
    }
}
