using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Audio.ISamplePeak;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls
{
    [Export(typeof(IWaveformDrawing))]
    public class DefaultWaveformDrawing : CommonWaveformDrawingBase
    {
        private readonly ISimpleLineDrawing lineDrawing;

        public DefaultWaveformDrawing()
        {
            lineDrawing = IoC.Get<ISimpleLineDrawing>();
        }

        public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData)
        {
            var width = target.ViewWidth;
            var height = target.ViewHeight;

            var curTime = target.CurrentTime;
            var fromTime = curTime - TimeSpan.FromMilliseconds(target.CurrentTimeXOffset * target.DurationMsPerPixel);
            var toTime = fromTime + TimeSpan.FromMilliseconds(width * target.DurationMsPerPixel);

            var durationMs = (toTime - fromTime).TotalMilliseconds;

            (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

            lineDrawing.Begin(target, 1);
            for (int i = minIndex; i < maxIndex; i++)
            {
                var peakPoint = peakData[i];

                var x = width * ((peakPoint.Time - fromTime).TotalMilliseconds / durationMs) - width / 2;
                var yTop = height / 2 * peakPoint.Amplitudes[0];
                var yButtom = -height / 2 * peakPoint.Amplitudes[1];

                lineDrawing.PostPoint(new((float)x, 0), new(1, 0, 1, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new((float)x, yTop), new(1, 0, 1, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new((float)x, yButtom), new(1, 1, 0, 1), VertexDash.Solider);
            }
            lineDrawing.End();

            //draw currenttime indirector
            lineDrawing.Begin(target, 2);
            var ix = width * ((curTime - fromTime).TotalMilliseconds / durationMs) - width / 2;
            lineDrawing.PostPoint(new((float)ix - 2, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            lineDrawing.PostPoint(new((float)ix - 2, +height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            lineDrawing.PostPoint(new((float)ix + 2, +height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            lineDrawing.PostPoint(new((float)ix + 2, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            lineDrawing.PostPoint(new((float)ix - 2, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            lineDrawing.End();
        }
    }
}
