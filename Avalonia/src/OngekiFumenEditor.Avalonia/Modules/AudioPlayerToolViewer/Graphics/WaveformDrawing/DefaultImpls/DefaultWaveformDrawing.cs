using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.Numerics;
using NumericsVector4 = System.Numerics.Vector4;

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

    private static readonly NumericsVector4 IndirectorColor = new(1, 1, 0, 1);
    private static readonly NumericsVector4 BeatColor = new(1, 0, 0, 1);
    private static readonly ILineDrawing.VertexDash SoliderDash = ILineDrawing.VertexDash.Solider;
    private static readonly NumericsVector4 ObjectPlaceColor = new(1, 1, 0, 1);
    private static readonly NumericsVector4 HoldColor = new(1, 1, 0, 0.75f);
    private static readonly NumericsVector4 WaveformFillColor = new(100 / 255f, 149 / 255f, 237 / 255f, 1);
    private static readonly NumericsVector4 TransparentColor = new(0, 0, 0, 0);
    private static readonly NumericsVector4 WhiteColor = new(1, 1, 1, 1);

    private readonly List<(float X, string Text)> cachedPostDrawList = [];
    private readonly List<ILineDrawing.LineVertex> cachedLineDrawList = [];
    private readonly List<CircleInstance> cachedCircleDrawList = [];
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

    public override void Draw(IWaveformDrawingContext target, PeakPointCollection peakData, IDrawCommandListBuilder builder)
    {
        if (!isInitialized
            || !WaveformGeometry.TryCreateViewport(
                target.CurrentDrawingTargetContext.ViewRelativeRect.Width,
                target.CurrentDrawingTargetContext.ViewRelativeRect.Height,
                target.CurrentTime,
                target.CurrentTimeXOffset,
                target.DurationMsPerPixel,
                out var viewport))
        {
            return;
        }

        cachedPostDrawList.Clear();
        string currentTimeText = null;

        //绘制波形
        if (option.ShowWaveform && peakData is not null && peakData.Count != 0)
            DrawWaveform(builder, target, peakData, viewport);

        //绘制节奏线
        if (target.EditorViewModel is FumenVisualEditorViewModel editor && editor.EditorContext.Fumen is not null)
            currentTimeText = DrawEditorOverlays(builder, target, editor, viewport);

        //绘制当前播放时间游标
        DrawCurrentTimeIndicator(builder, viewport);

        if (cachedPostDrawList.Count > 0 || !string.IsNullOrEmpty(currentTimeText))
            DrawOverlayText(builder, viewport, currentTimeText);
    }

    private void DrawWaveform(
        IDrawCommandListBuilder builder,
        IWaveformDrawingContext target,
        PeakPointCollection peakData,
        WaveformViewport viewport)
    {
        (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(viewport.FromTime, viewport.ToTime);

        cachedLineDrawList.Clear();
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
                cachedLineDrawList.Add(new(new(x, top), WaveformFillColor, SoliderDash));
                hasPoint = true;
            }
            else
            {
                cachedLineDrawList.Add(new(new(x, top), WaveformFillColor, SoliderDash));
            }
            cachedLineDrawList.Add(new(new(x, bottom), WaveformFillColor, SoliderDash));
        }

        if (hasPoint)
            builder.DrawSimpleLines(cachedLineDrawList, 1);
        cachedLineDrawList.Clear();
    }

    private string DrawEditorOverlays(
        IDrawCommandListBuilder builder,
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
            editor.EditorContext.Fumen.BpmList,
            editor.EditorContext.Fumen.MeterChanges);

        cachedObjTimeMap.Clear();
        if (option.ShowObjectPlaceLine)
            DrawObjectPlaceLines(builder, editor, viewport, beginTGrid, endTGrid);

        if (option.ShowTimingLine)
            DrawTimingLines(builder, editor, viewport, beginTime, endTime, target.CurrentTime, currentMeter, currentBpm);

        cachedObjTimeMap.Clear();
        return $"{currentMeter.BunShi}/{currentMeter.Bunbo} BPM:{currentBpm.BPM}";
    }

    private void DrawObjectPlaceLines(
        IDrawCommandListBuilder builder,
        FumenVisualEditorViewModel editor,
        WaveformViewport viewport,
        TGrid beginTGrid,
        TGrid endTGrid)
    {
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

        var fumen = editor.EditorContext.Fumen;
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

        cachedLineDrawList.Clear();
        foreach (var hold in fumen.Holds.GetVisibleStartObjects(beginTGrid, endTGrid))
        {
            var previousType = cachedObjTimeMap.TryGetValue(hold.TGrid, out var value)
                ? value
                : ObjType.None;
            cachedObjTimeMap[hold.TGrid] = previousType | ObjType.Default;
            if (hold?.HoldEnd?.TGrid is not TGrid end)
                continue;

            var fromX = CalculateX(hold.TGrid);
            var toX = CalculateX(end);
            //连线首尾各放一个透明端点，避免与前后线段误连
            cachedLineDrawList.Add(new(new(fromX, 0), TransparentColor, SoliderDash));
            cachedLineDrawList.Add(new(new(fromX, 0), HoldColor, SoliderDash));
            cachedLineDrawList.Add(new(new(toX, 0), HoldColor, SoliderDash));
            cachedLineDrawList.Add(new(new(toX, 0), TransparentColor, SoliderDash));
        }
        builder.DrawSimpleLines(cachedLineDrawList, 4);
        cachedLineDrawList.Clear();

        const float beatHeightWeight = 0.75f;
        var topY = viewport.Height / 2 * beatHeightWeight;
        var bottomY = -topY;

        cachedLineDrawList.Clear();
        cachedCircleDrawList.Clear();
        foreach (var (tGrid, type) in cachedObjTimeMap)
        {
            var x = CalculateX(tGrid);
            if (type.HasFlag(ObjType.Default))
            {
                cachedLineDrawList.Add(new(new(x, bottomY), TransparentColor, SoliderDash));
                cachedLineDrawList.Add(new(new(x, bottomY), ObjectPlaceColor, SoliderDash));
                cachedLineDrawList.Add(new(new(x, topY), ObjectPlaceColor, SoliderDash));
                cachedLineDrawList.Add(new(new(x, topY), TransparentColor, SoliderDash));
            }

            if (type.HasFlag(ObjType.Bullet))
                cachedCircleDrawList.Add(new CircleInstance(new(x, bottomY - 10), new(1, 0, 1, 1), true, 5f, 0));

            if (type.HasFlag(ObjType.Bell))
                cachedCircleDrawList.Add(new CircleInstance(new(x, topY + 10), new(1, 1, 0, 1), true, 5f, 0));

            if (type.HasFlag(ObjType.Flick))
            {
                //todo
            }
        }
        builder.DrawSimpleLines(cachedLineDrawList, 2);
        builder.DrawCircles(cachedCircleDrawList);
        cachedLineDrawList.Clear();
        cachedCircleDrawList.Clear();
    }

    private void DrawTimingLines(
        IDrawCommandListBuilder builder,
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
        var bpmList = editor.EditorContext.Fumen.BpmList;

        cachedLineDrawList.Clear();
        foreach ((var tGrid, var timeMilliseconds, var beatIndex, var meter, var bpm) in
            TGridCalculator.GetVisbleTimelines_DesignMode(
                dummySoflanList,
                bpmList,
                editor.EditorContext.Fumen.MeterChanges,
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
            cachedLineDrawList.Add(new(new(x, -topY), TransparentColor, SoliderDash));
            cachedLineDrawList.Add(new(new(x, -topY), BeatColor, SoliderDash));
            cachedLineDrawList.Add(new(new(x, topY), BeatColor, SoliderDash));
            cachedLineDrawList.Add(new(new(x, topY), TransparentColor, SoliderDash));

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
        builder.DrawSimpleLines(cachedLineDrawList, 2);
        cachedLineDrawList.Clear();
    }

    private static void DrawCurrentTimeIndicator(IDrawCommandListBuilder builder, WaveformViewport viewport)
    {
        var left = viewport.CurrentTimeX - 1.5f;
        var right = viewport.CurrentTimeX + 1.5f;
        var top = viewport.Height / 2;
        var bottom = -top;

        builder.DrawPolygon(Primitive.TriangleStrip,
        [
            new PolygonVertex(new(left, bottom), IndirectorColor),
            new PolygonVertex(new(right, bottom), IndirectorColor),
            new PolygonVertex(new(left, top), IndirectorColor),
            new PolygonVertex(new(right, top), IndirectorColor),
        ]);
    }

    private void DrawOverlayText(IDrawCommandListBuilder builder, WaveformViewport viewport, string currentTimeText)
    {
        // 文字与波形/拍线/游标共用同一套坐标：原点在视口中心、y 轴朝上，±Height/2 就是上下边界。
        // 所以这里给的是居中坐标 —— 不能再叠加 Width/2，纵向也不能用自顶向下的像素值。
        // origin 取 (0, 0.5)：pos 即文本框左边中点，与编辑器其它左对齐标签同一口径。
        var bottomMiddleY = -viewport.Height / 2 + 9f;
        var topMiddleY = viewport.Height / 2 - 10f;

        //绘制提示
        foreach (var (x, text) in cachedPostDrawList)
            builder.DrawString(text, new Vector2(x, bottomMiddleY), Vector2.One, 15, 0, IndirectorColor, new Vector2(0, 0.5f), IStringDrawing.StringStyle.Normal, default);

        if (!string.IsNullOrEmpty(currentTimeText))
            builder.DrawString(currentTimeText, new Vector2(viewport.CurrentTimeX + 4, topMiddleY), Vector2.One, 15, 0, IndirectorColor, new Vector2(0, 0.5f), IStringDrawing.StringStyle.Normal, default);
    }
}
