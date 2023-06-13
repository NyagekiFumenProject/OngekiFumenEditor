using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.ISamplePeak;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    public interface IWaveformDrawing : IDrawing
    {
        IWaveformDrawingOption Options { get; }
        void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak);
    }
}
