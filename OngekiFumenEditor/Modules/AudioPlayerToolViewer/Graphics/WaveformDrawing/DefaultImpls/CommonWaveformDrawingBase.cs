using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.ISamplePeak;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    public abstract class CommonWaveformDrawingBase : CommonDrawingBase, IWaveformDrawing
    {
        public abstract IWaveformDrawingOption Options { get; }
        public abstract void Draw(IWaveformDrawingContext target, PeakPointCollection samplePeak);
    }
}
