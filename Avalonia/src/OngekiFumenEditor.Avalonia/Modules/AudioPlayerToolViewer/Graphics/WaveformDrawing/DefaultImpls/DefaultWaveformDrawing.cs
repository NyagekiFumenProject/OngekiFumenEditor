using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;

[RegisterSingleton<IWaveformDrawing>]
public class DefaultWaveformDrawing : CommonWaveformDrawingBase
{
    private ISimpleLineDrawing lineDrawing;
    private readonly DefaultWaveformOption option = new();
    public override IWaveformDrawingOption Options => option;

    public override void Initialize(IRenderManagerImpl impl)
    {
        lineDrawing = impl.SimpleLineDrawing;
    }

    public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData)
    {
        if (!option.ShowWaveform || peakData is null || peakData.Count == 0 || lineDrawing is null)
            return;

        var width = (float)target.CurrentDrawingTargetContext.Rect.Width;
        var height = (float)target.CurrentDrawingTargetContext.Rect.Height;
        var curTime = target.CurrentTime;
        var fromTime = curTime - TimeSpan.FromMilliseconds(target.CurrentTimeXOffset * target.DurationMsPerPixel);
        var toTime = fromTime + TimeSpan.FromMilliseconds(width * target.DurationMsPerPixel);
        var durationMs = Math.Max(1, (toTime - fromTime).TotalMilliseconds);

        (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

        lineDrawing.Begin(target, 1);
        for (int i = minIndex; i < maxIndex; i++)
        {
            var peakPoint = peakData[i];
            var x = (float)(width * ((peakPoint.Time - fromTime).TotalMilliseconds / durationMs) - width / 2);
            var top = height / 2 * peakPoint.Amplitudes[0];
            var bottom = -height / 2 * peakPoint.Amplitudes[1];
            lineDrawing.PostPoint(new Vector2(x, top), new Vector4(0.39f, 0.58f, 0.93f, 1), ILineDrawing.VertexDash.Solider);
            lineDrawing.PostPoint(new Vector2(x, bottom), new Vector4(0.39f, 0.58f, 0.93f, 1), ILineDrawing.VertexDash.Solider);
        }
        lineDrawing.End();
    }
}
