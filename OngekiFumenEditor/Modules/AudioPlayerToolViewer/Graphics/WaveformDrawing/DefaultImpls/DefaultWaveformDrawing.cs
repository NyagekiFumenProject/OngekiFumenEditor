using Caliburn.Micro;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
        private static readonly Matrix4 WaveformScale = Matrix4.CreateScale(1, 0.70f, 1f);
        private static readonly VertexDash InvailedLineDash = new VertexDash() { DashSize = 2, GapSize = 2 };

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

            //绘制波形
            lineDrawing.PushOverrideModelMatrix(lineDrawing.GetOverrideModelMatrix() * WaveformScale);
            lineDrawing.Begin(target, 1);
            {
                var prevX = 0f;
                lineDrawing.PostPoint(new(-width / 2, 0), new(1, 1, 1, 1), InvailedLineDash);
                for (int i = minIndex; i < maxIndex; i++)
                {
                    var peakPoint = peakData[i];

                    var x = (float)(width * ((peakPoint.Time - fromTime).TotalMilliseconds / durationMs) - width / 2);
                    var yTop = height / 2 * peakPoint.Amplitudes[0];
                    var yButtom = -height / 2 * peakPoint.Amplitudes[1];

                    lineDrawing.PostPoint(new(x, 0), new(1, 0, 1, 1), VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, yTop), new(1, 0, 1, 1), VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, yButtom), new(1, 1, 0, 1), VertexDash.Solider);
                    prevX = x;
                }
                lineDrawing.PostPoint(new(prevX, 0), new(1, 1, 0, 1), InvailedLineDash);
                lineDrawing.PostPoint(new(width / 2, 0), new(1, 1, 1, 1), InvailedLineDash);
            }
            lineDrawing.End();
            lineDrawing.PopOverrideModelMatrix(out _);

            //绘制当前播放时间游标
            lineDrawing.Begin(target, 2);
            {
                var ix = (float)(width * ((curTime - fromTime).TotalMilliseconds / durationMs) - width / 2);
                lineDrawing.PostPoint(new(ix - 1.5f, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new(ix - 1.5f, +height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new(ix + 1.5f, +height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new(ix + 1.5f, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                lineDrawing.PostPoint(new(ix - 1.5f, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
            }
            lineDrawing.End();

            //todo 绘制节奏线
            if (target.EditorViewModel is FumenVisualEditorViewModel editor)
            {
                var beginTime = fromTime.TotalSeconds < 0 ? TimeSpan.Zero : fromTime;
                var endTime = toTime > target.AudioTotalDuration ? target.AudioTotalDuration : toTime;

                var beginX = TGridCalculator.ConvertAudioTimeToY(beginTime, target.EditorViewModel);
                var endX = TGridCalculator.ConvertAudioTimeToY(endTime, target.EditorViewModel);
                var curX = TGridCalculator.ConvertAudioTimeToY(curTime, target.EditorViewModel);

                var aWidth = (endTime - beginTime).TotalMilliseconds / target.DurationMsPerPixel;
                var prefixOffsetX = -Math.Min(0, fromTime.TotalMilliseconds) / target.DurationMsPerPixel;
                var xWidth = endX - beginX;

                lineDrawing.Begin(target, 1);
                foreach ((_, var bx, var beatIdx) in TGridCalculator.GetVisbleTimelines(editor.Fumen.BpmList,
                    editor.Fumen.MeterChanges, beginX, endX, curX, editor.Setting.BeatSplit, 1, editor.Setting.TGridUnitLength))
                {
                    var x = (float)(prefixOffsetX + aWidth * ((bx - beginX) / xWidth) - width / 2);

                    lineDrawing.PostPoint(new(x, -height / 2), new(0, 0, 0, 0), VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, -height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, height / 2), new(1, 1, 0, 1), VertexDash.Solider);
                    lineDrawing.PostPoint(new(x, height / 2), new(0, 0, 0, 0), VertexDash.Solider);
                }
                lineDrawing.End();
            }
        }
    }
}
