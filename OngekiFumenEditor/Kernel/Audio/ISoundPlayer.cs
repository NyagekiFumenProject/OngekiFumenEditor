using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public interface ISoundPlayer : IDisposable
    {
        float Volume { get; set; }
        TimeSpan Duration { get; }

        void PlayOnce();

        void PlayLoop(int loopId,TimeSpan init);
        void StopLoop(int loopId);
    }
}
