using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics
{
    public interface IWaveformDrawingContext : IDrawingContext
    {
        /// <summary>
        /// 表示当前时间
        /// </summary>
        public TimeSpan CurrentTime { get; }
        /// <summary>
        /// 表示每个像素显示时间间距
        /// </summary>
        public float DurationMsPerPixel { get; }

        public float CurrentTimeXOffset { get; }
    }
}
