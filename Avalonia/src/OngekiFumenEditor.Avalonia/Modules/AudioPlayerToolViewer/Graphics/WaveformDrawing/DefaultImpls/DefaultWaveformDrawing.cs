using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using SkiaSharp;

namespace OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;

[RegisterSingleton<IWaveformDrawing>]
public class DefaultWaveformDrawing : CommonWaveformDrawingBase
{
    [Flags]
    private enum ObjType
    {
        None = 0,
        Default = 1,
        Bullet = 2,
        Bell = 4,
        Flick = 8,
    }

    private static readonly SKColor IndirectorColor = new(255, 255, 0);
    private static readonly SKColor BeatColor = new(255, 0, 0);
    private static readonly SKColor ObjectPlaceColor = new(255, 255, 0);
    private static readonly SKColor HoldColor = new(255, 255, 0, 191);
    private static readonly SKColor WaveformFillColor = new(100, 149, 237);

    private readonly List<(float X, string Text)> cachedPostDrawList = [];
    private readonly List<(SKPoint Point, SKColor Color)> cachedCircleDrawList = [];
    private readonly Dictionary<TGrid, ObjType> cachedObjTimeMap = [];
    private readonly DefaultWaveformOption option;
    private SoflanList dummySoflanList;
    private bool isInitialized;

    public override IWaveformDrawingOption Options => option;

    public DefaultWaveformDrawing() : this(new DefaultWaveformOption())
    {
    }

    internal DefaultWaveformDrawing(DefaultWaveformOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        this.option = option;
    }

    public override void Initialize(IRenderManagerImpl impl)
    {
        if (impl is not DefaultSkiaDrawingManagerImpl)
            throw new NotSupportedException("Waveform rendering requires the Avalonia Skia render manager.");

        dummySoflanList = new SoflanList();
        isInitialized = true;
    }

    public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData)
    {
        if (!isInitialized
            || target.RenderContext is not DefaultSkiaRenderContext { Canvas: { } canvas }
            || !WaveformGeometry.TryCreateViewport(
                target.CurrentDrawingTargetContext.Rect.Width,
                target.CurrentDrawingTargetContext.Rect.Height,
                target.CurrentTime,
                target.CurrentTimeXOffset,
                target.DurationMsPerPixel,
                out var viewport))
        {
            return;
        }

        cachedPostDrawList.Clear();
        string currentTimeText = null;
        canvas.Save();
        try
        {
            canvas.Translate(viewport.Width / 2, viewport.Height / 2);
            canvas.Scale(1, -1);

            //绘制波形
            if (option.ShowWaveform && peakData is not null && peakData.Count != 0)
                DrawWaveform(canvas, target, peakData, viewport);

            //绘制节奏线
            if (target.EditorViewModel is FumenVisualEditorViewModel editor && editor.Fumen is not null)
                currentTimeText = DrawEditorOverlays(canvas, target, editor, viewport);

            //绘制当前播放时间游标
            DrawCurrentTimeIndicator(canvas, viewport);
        }
        finally
        {
            canvas.Restore();
        }

        DrawOverlayText(canvas, viewport, currentTimeText);
    }

    private static void DrawWaveform(
        SKCanvas canvas,
        IWaveformDrawingContext target,
        PeakPointCollection peakData,
        WaveformViewport viewport)
    {
        (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(viewport.FromTime, viewport.ToTime);
        using var path = new SKPath();
        var hasPoint = false;

        for (var i = minIndex; i < maxIndex; i++)
        {
            var peakPoint = peakData[i];
            if (!WaveformGeometry.TryGetVerticalExtents(
                peakPoint.Amplitudes,
                viewport.Height,
                target.WaveformVecticalScale,
                out var top,
                out var bottom))
            {
                continue;
            }

            var x = viewport.ProjectX(peakPoint.Time);
            // 单声道在中心线两侧镜像；双声道分别占据上、下半区。
            if (!hasPoint)
            {
                path.MoveTo(x, top);
                hasPoint = true;
            }
            else
            {
                path.LineTo(x, top);
            }
            path.LineTo(x, bottom);
        }

        if (!hasPoint)
            return;

        using var paint = CreateStrokePaint(WaveformFillColor, 1);
        canvas.DrawPath(path, paint);
    }

    private string DrawEditorOverlays(
        SKCanvas canvas,
        IWaveformDrawingContext target,
        FumenVisualEditorViewModel editor,
        WaveformViewport viewport)
    {
        var beginTime = viewport.FromTime < TimeSpan.Zero ? TimeSpan.Zero : viewport.FromTime;
        var endTime = viewport.ToTime > target.AudioTotalDuration
            ? target.AudioTotalDuration
            : viewport.ToTime;
        if (endTime < beginTime)
            return null;

        var beginTGrid = TGridCalculator.ConvertAudioTimeToTGrid(beginTime, editor);
        var endTGrid = TGridCalculator.ConvertAudioTimeToTGrid(endTime, editor);
        var currentTGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentTime, editor);
        (_, _, var currentMeter, var currentBpm) = TGridCalculator.GetCurrentTimeSignature(
            currentTGrid,
            editor.Fumen.BpmList,
            editor.Fumen.MeterChanges);

        cachedObjTimeMap.Clear();
        if (option.ShowObjectPlaceLine)
            DrawObjectPlaceLines(canvas, editor, viewport, beginTGrid, endTGrid);

        if (option.ShowTimingLine)
            DrawTimingLines(canvas, editor, viewport, beginTime, endTime, target.CurrentTime, currentMeter, currentBpm);

        cachedObjTimeMap.Clear();
        return $"{currentMeter.BunShi}/{currentMeter.Bunbo} BPM:{currentBpm.BPM}";
    }

    private void DrawObjectPlaceLines(
        SKCanvas canvas,
        FumenVisualEditorViewModel editor,
        WaveformViewport viewport,
        TGrid beginTGrid,
        TGrid endTGrid)
    {
        cachedCircleDrawList.Clear();

        void ApplyObjectCounting(IEnumerable<ITimelineObject> timelineObjects, ObjType type)
        {
            foreach (var timeObject in timelineObjects)
            {
                var previousType = cachedObjTimeMap.TryGetValue(timeObject.TGrid, out var value)
                    ? value
                    : ObjType.None;
                cachedObjTimeMap[timeObject.TGrid] = type | previousType;
            }
        }

        var fumen = editor.Fumen;
        ApplyObjectCounting(fumen.Taps.BinaryFindRange(beginTGrid, endTGrid), ObjType.Default);
        ApplyObjectCounting(fumen.Bullets.BinaryFindRange(beginTGrid, endTGrid), ObjType.Bullet);
        ApplyObjectCounting(fumen.Bells.BinaryFindRange(beginTGrid, endTGrid), ObjType.Bell);
        ApplyObjectCounting(fumen.Beams.GetVisibleStartObjects(beginTGrid, endTGrid), ObjType.Default);
        ApplyObjectCounting(fumen.Flicks.BinaryFindRange(beginTGrid, endTGrid), ObjType.Flick);

        float CalculateX(TGrid tGrid)
        {
            var time = TGridCalculator.ConvertTGridToAudioTime(tGrid, editor);
            return viewport.ProjectX(time);
        }

        using (var holdPaint = CreateStrokePaint(HoldColor, 4))
        {
            foreach (var hold in fumen.Holds.GetVisibleStartObjects(beginTGrid, endTGrid))
            {
                var previousType = cachedObjTimeMap.TryGetValue(hold.TGrid, out var value)
                    ? value
                    : ObjType.None;
                cachedObjTimeMap[hold.TGrid] = previousType | ObjType.Default;
                if (hold?.HoldEnd?.TGrid is not TGrid end)
                    continue;

                canvas.DrawLine(CalculateX(hold.TGrid), 0, CalculateX(end), 0, holdPaint);
            }
        }

        const float beatHeightWeight = 0.75f;
        var topY = viewport.Height / 2 * beatHeightWeight;
        var bottomY = -topY;
        using (var objectPaint = CreateStrokePaint(ObjectPlaceColor, 2))
        {
            foreach (var (tGrid, type) in cachedObjTimeMap)
            {
                var x = CalculateX(tGrid);
                if (type.HasFlag(ObjType.Default))
                    canvas.DrawLine(x, bottomY, x, topY, objectPaint);

                if (type.HasFlag(ObjType.Bullet))
                    cachedCircleDrawList.Add((new(x, bottomY - 10), new(255, 0, 255)));
                if (type.HasFlag(ObjType.Bell))
                    cachedCircleDrawList.Add((new(x, topY + 10), new(255, 255, 0)));
                if (type.HasFlag(ObjType.Flick))
                {
                    //todo
                }
            }
        }

        foreach (var (point, color) in cachedCircleDrawList)
        {
            using var circlePaint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(point, 5, circlePaint);
        }
    }

    private void DrawTimingLines(
        SKCanvas canvas,
        FumenVisualEditorViewModel editor,
        WaveformViewport viewport,
        TimeSpan beginTime,
        TimeSpan endTime,
        TimeSpan currentTime,
        MeterChange currentMeter,
        BPMChange currentBpm)
    {
        cachedPostDrawList.Clear();
        var previousMeter = currentMeter;
        var previousBpm = currentBpm;
        var bpmList = editor.Fumen.BpmList;
        using var beatPaint = CreateStrokePaint(BeatColor, 2);

        foreach ((var tGrid, var timeMilliseconds, var beatIndex, var meter, var bpm) in
            TGridCalculator.GetVisbleTimelines_DesignMode(
                dummySoflanList,
                bpmList,
                editor.Fumen.MeterChanges,
                beginTime.TotalMilliseconds,
                endTime.TotalMilliseconds,
                currentTime.TotalMilliseconds,
                editor.Setting.BeatSplit,
                1f))
        {
            var x = viewport.ProjectX(TimeSpan.FromMilliseconds(timeMilliseconds));
            var beatHeightWeight = beatIndex == 0 ? 0.75f : 0.5f;
            beatHeightWeight = cachedObjTimeMap.ContainsKey(tGrid) ? 0.1f : beatHeightWeight;
            var topY = viewport.Height / 2 * beatHeightWeight;
            canvas.DrawLine(x, -topY, x, topY, beatPaint);

            var text = string.Empty;
            if (previousMeter != meter)
                text += $"{meter.BunShi}/{meter.Bunbo}";
            if (previousBpm != bpm)
                text += $" BPM:{bpm.BPM}";
            if (text.Length > 0)
                cachedPostDrawList.Add((x + 2, text));

            previousMeter = meter;
            previousBpm = bpm;
        }
    }

    private static void DrawCurrentTimeIndicator(SKCanvas canvas, WaveformViewport viewport)
    {
        using var paint = CreateStrokePaint(IndirectorColor, 2);
        canvas.DrawRect(
            new SKRect(
                viewport.CurrentTimeX - 1.5f,
                -viewport.Height / 2,
                viewport.CurrentTimeX + 1.5f,
                viewport.Height / 2),
            paint);
    }

    private void DrawOverlayText(
        SKCanvas canvas,
        WaveformViewport viewport,
        string currentTimeText)
    {
        if (cachedPostDrawList.Count == 0 && string.IsNullOrEmpty(currentTimeText))
            return;

        using var paint = new SKPaint
        {
            Color = IndirectorColor,
            IsAntialias = true
        };
        using var font = new SKFont
        {
            Typeface = SKTypeface.Default,
            Size = 15
        };

        //绘制提示
        foreach (var (x, text) in cachedPostDrawList)
            canvas.DrawText(text, x + viewport.Width / 2, viewport.Height - 4, font, paint);

        if (!string.IsNullOrEmpty(currentTimeText))
        {
            canvas.DrawText(
                currentTimeText,
                viewport.CurrentTimeX + viewport.Width / 2 + 4,
                16,
                font,
                paint);
        }
    }

    private static SKPaint CreateStrokePaint(SKColor color, float width)
    {
        return new()
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width
        };
    }
}
