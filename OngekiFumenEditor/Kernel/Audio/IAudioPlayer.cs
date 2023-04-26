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
        TimeSpan CurrentTime { get; }

        /// <summary>
        /// 音量,0~1
        /// </summary>
        float Volume { get; set; }

        /// <summary>
        /// 总长度,毫秒
        /// </summary>
        TimeSpan Duration { get; }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 播放器是否可用/可操作
        /// </summary>
        bool IsAvaliable { get; }

        void Play();
        void Stop();
        void Pause();
        void Seek(TimeSpan TimeSpan, bool pause);

        Task<SampleData> GetSamplesAsync();
    }
}
