using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio
{
    public interface IAudioPlayer : IDisposable, INotifyPropertyChanged
    {
        /// <summary>
        /// 当前播放时间,毫秒
        /// </summary>
        float CurrentTime { get; }
        float Volume { get; set; }
        /// <summary>
        /// 总长度,毫秒
        /// </summary>
        float Duration { get; }
        bool IsPlaying { get; }

        void Play();
        void Stop();
        void Pause();
        void Seek(float time, bool pause);
    }
}
