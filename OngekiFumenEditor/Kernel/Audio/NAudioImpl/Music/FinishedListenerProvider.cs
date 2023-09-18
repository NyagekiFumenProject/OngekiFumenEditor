using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.NAudioImpl.Music
{
    internal class FinishedListenerProvider : ISampleProvider
    {
        public ISampleProvider Provider { get; set; }

        private bool enableEventFire;

        public event Action OnReturnEmptySamples;

        public WaveFormat WaveFormat => Provider.WaveFormat;

        public void StartListen()
        {
            enableEventFire = true;
        }

        public void StopListen()
        {
            enableEventFire = false;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var read = Provider.Read(buffer, offset, count);
            if (read == 0 && enableEventFire)
                OnReturnEmptySamples?.Invoke();
            return read;
        }
    }
}
