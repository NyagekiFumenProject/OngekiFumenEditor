using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

public static class TGridCalculator
{
    public const float FRAME_DURATION = 16.666666f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan ConvertFrameToAudioTime(float frame) =>
        TimeSpan.FromMilliseconds(FRAME_DURATION * frame);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, FumenVisualEditorViewModel editor) =>
        ConvertAudioTimeToTGrid(audioTime, editor.Fumen.BpmList);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList)
    {
        var positionBpmList = GetAllBpmUniformPositionList(bpmList);
        var pos = positionBpmList.LastOrDefault(x => x.audioTime <= audioTime);
        if (pos.bpm is null)
            return default;

        var relativeBpmLenOffset = pos.bpm.LengthConvertToOffset((audioTime - pos.audioTime).TotalMilliseconds);
        return pos.bpm.TGrid + relativeBpmLenOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, FumenVisualEditorViewModel editor) =>
        ConvertTGridToAudioTime(tGrid, editor.Fumen.BpmList);

    public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
    {
        var positionBpmList = GetAllBpmUniformPositionList(bpmList);
        var pos = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
        if (pos.bpm is null)
        {
            if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
                return TimeSpan.FromMilliseconds(0);
            return default;
        }

        var relativeBpmLenOffset = TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(pos.bpm, tGrid));
        return pos.audioTime + relativeBpmLenOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TGrid ConvertYToTGrid_DesignMode(double pickY, FumenVisualEditorViewModel editor) =>
        ConvertYToTGrid_DesignMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    public static TGrid ConvertYToTGrid_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
    {
        pickY /= scale;
        var list = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList);
        var pos = list.LastOrDefault(x => x.Y <= pickY);
        if (pos.Bpm is null)
            return default;

        var absSpeed = Math.Abs(pos.Speed);
        var relativeBpmLenOffset = pos.Bpm.LengthConvertToOffset((pickY - pos.Y) / absSpeed);
        return pos.TGrid + relativeBpmLenOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, FumenVisualEditorViewModel editor) =>
        ConvertYToAudioTime_DesignMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    private static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
    {
        var tGrid = ConvertYToTGrid_DesignMode(pickY, soflanList, bpmList, scale);
        if (tGrid is null)
            return default;
        return ConvertTGridToAudioTime(tGrid, bpmList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, FumenVisualEditorViewModel editor) =>
        ConvertTGridToY_DesignMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale) =>
        ConvertTGridToY_DesignMode(ConvertAudioTimeToTGrid(audioTime, bpmList), soflanList, bpmList, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridToY_DesignMode(TGrid tGrid, FumenVisualEditorViewModel editor) =>
        ConvertTGridToY_DesignMode(tGrid, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridToY_DesignMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale) =>
        ConvertTGridUnitToY_DesignMode(tGrid.TotalUnit, soflanList, bpmList, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, FumenVisualEditorViewModel editor) =>
        ConvertTGridUnitToY_DesignMode(tGridUnit, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
    {
        var pos = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList)
            .LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
        if (pos.Bpm is null)
            return default;

        var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);
        var absSpeed = Math.Abs(pos.Speed);
        return (pos.Y + relativeBpmLenOffset * absSpeed) * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, FumenVisualEditorViewModel editor) =>
        ConvertYToTGrid_PreviewMode(pickY, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
    {
        var ranges = soflanList.GetVisibleRanges_PreviewMode(pickY, 0, 0, bpmList, scale);
        return ranges.OrderBy(x => x.minTGrid).Select(x => x.minTGrid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridToY_PreviewMode(TGrid tGrid, FumenVisualEditorViewModel editor) =>
        ConvertTGridToY_PreviewMode(tGrid, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale) =>
        ConvertTGridUnitToY_PreviewMode(tGrid.TotalUnit, soflanList, bpmList, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, FumenVisualEditorViewModel editor) =>
        ConvertTGridUnitToY_PreviewMode(tGridUnit, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);

    public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
    {
        var pos = soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList)
            .LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
        if (pos.Bpm is null)
            return default;

        var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);
        var speed = pos.Speed;
        return (pos.Y + relativeBpmLenOffset * speed) * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, FumenVisualEditorViewModel editor) =>
        ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale) =>
        ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, bpmList), soflanList, bpmList, scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(FumenVisualEditorViewModel editor) =>
        GetVisbleTimelines_PreviewMode(editor.CurrentDrawingTargetContext.CurrentSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges,
            editor.CurrentDrawingTargetContext.Rect.MinY, editor.CurrentDrawingTargetContext.Rect.MaxY,
            editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

    public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(
        SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double currentY, double viewHeight, double judgeLineOffsetY, int beatSplit, double scale)
    {
        var tGridRanges = soflans.GetVisibleRanges_PreviewMode(currentY, viewHeight, judgeLineOffsetY, bpmList, scale);

        foreach (var range in tGridRanges)
        {
            var rMinY = ConvertTGridToY_DesignMode(range.minTGrid, soflans, bpmList, scale);
            var rMaxY = ConvertTGridToY_DesignMode(range.maxTGrid, soflans, bpmList, scale);

            var queryFromDesignMode = GetVisbleTimelines_DesignMode(soflans, bpmList, meterList, rMinY, rMaxY, judgeLineOffsetY, 1, scale);
            foreach (var item in queryFromDesignMode)
            {
                if (item.beatIndex != 0)
                    continue;

                var cpItem = item;
                cpItem.y = ConvertTGridToY_PreviewMode(cpItem.tGrid, soflans, bpmList, scale);
                yield return cpItem;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(FumenVisualEditorViewModel editor) =>
        GetVisbleTimelines_DesignMode(editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges,
            editor.RectInDesignMode.MinY, editor.RectInDesignMode.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

    public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(
        SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale)
    {
        minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);
        var minVisibleCanvasTGrid = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale);
        var endTGrid = ConvertYToTGrid_DesignMode(maxVisibleCanvasY, soflans, bpmList, scale);
        var currentTGridBaseOffset = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale)
                                     ?? ConvertYToTGrid_DesignMode(minVisibleCanvasY + judgeLineOffsetY, soflans, bpmList, 1);

        var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
        var currentTimeSignatureIndex = timeSignatures.LastOrDefaultIndexByBinarySearch(minVisibleCanvasTGrid, x => x.startTGrid);
        var currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

        if (endTGrid is null)
            yield break;

        while (currentTGridBaseOffset is not null)
        {
            var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
            var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

            var (_, currentTGridBase, currentMeter, currentBpm) = currentTimeSignature;
            var (_, nextTGridBase, _, nextBpm) = nextTimeSignature;

            var resT = currentTGridBase.ResT;
            var beatCount = currentMeter.BunShi * beatSplit;
            var lengthPerBeat = resT * 1.0d / beatCount;

            var diff = currentTGridBaseOffset - currentTGridBase;
            var totalGrid = diff.Unit * resT + diff.Grid;
            var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

            if (beatCount == 0)
            {
                var y = ConvertTGridToY_DesignMode(currentTGridBase, soflans, bpmList, 1);
                yield return (currentTGridBase, y * scale, 0, currentMeter, currentBpm);
            }
            else
            {
                while (true)
                {
                    var tGrid = currentTGridBase + new GridOffset(0, (int)(lengthPerBeat * i));
                    var y = ConvertTGridToY_DesignMode(tGrid, soflans, bpmList, 1);

                    if (nextBpm is not null && tGrid >= nextTGridBase)
                        break;
                    if (tGrid > endTGrid)
                        yield break;
                    if (tGrid < currentTGridBaseOffset)
                    {
                        i++;
                        continue;
                    }

                    yield return (tGrid, y * scale, i % beatCount, currentMeter, currentBpm);
                    i++;
                }
            }

            currentTGridBaseOffset = nextTGridBase;
            currentTimeSignatureIndex = nextTimeSignatureIndex;
            currentTimeSignature = timeSignatures.Count > currentTimeSignatureIndex ? timeSignatures[currentTimeSignatureIndex] : default;
        }
    }

    public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, FumenVisualEditorViewModel editor) =>
        GetCurrentTimeSignature(tGrid, editor.Fumen.BpmList, editor.Fumen.MeterChanges);

    public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, BpmList bpmList, MeterChangeList meterList)
    {
        var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
        var idx = timeSignatures.BinarySearchBy(tGrid, x => x.startTGrid);
        idx = idx < 0 ? Math.Max(0, (~idx) - 1) : idx;
        return timeSignatures[idx];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor) =>
        GetAllBpmUniformPositionList(editor.Fumen.BpmList);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList) =>
        bpmList.GetCachedAllBpmUniformPositionList();

    public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale)
    {
        var resT = bpm.TGrid.ResT;
        var beatCount = meter.BunShi * beatSplit;
        var lengthPerBeat = resT * 1.0d / beatCount;
        return MathUtils.CalculateBPMLength(bpm, bpm.TGrid + new GridOffset(0, (int)lengthPerBeat)) * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime_DesignMode(
        float y, float range, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
    {
        var result = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y - range, y + range, 0, beatSplit, scale)
            .MinByOrDefault(x => Math.Abs(x.y - y));
        return (result.tGrid, result.y, result.beatIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, FumenVisualEditorViewModel editor) =>
        TryPickMagneticBeatTime_DesignMode(y, range, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges,
            editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, FumenVisualEditorViewModel editor) =>
        TryPickClosestBeatTime_DesignMode(y, editor.Fumen.SoflansMap.DefaultSoflanList, editor.Fumen.BpmList, editor.Fumen.MeterChanges,
            editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

    public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime_DesignMode(
        float y, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
    {
        var timeSignatures = meterChanges.GetCachedAllTimeSignatureUniformPositionList(bpmList);
        var tGrid = ConvertYToTGrid_DesignMode(y, soflans, bpmList, scale);
        if (tGrid is null)
            return default;

        var audioTime = ConvertTGridToAudioTime(tGrid, bpmList);
        var (prevAudioTime, _, meter, bpm) = timeSignatures.LastOrDefault(x => x.audioTime <= audioTime);
        var prevTGrid = ConvertAudioTimeToTGrid(prevAudioTime, bpmList);
        var prevY = ConvertTGridToY_DesignMode(prevTGrid, soflans, bpmList, scale);

        var downFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, prevY, y, 0, beatSplit, scale).LastOrDefault();
        var nextFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y, y + CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale), 0, beatSplit, scale).FirstOrDefault();

        if (Math.Abs(downFirst.y - y) < Math.Abs(nextFirst.y - y))
            return (downFirst.tGrid, downFirst.y, downFirst.beatIndex);
        return (nextFirst.tGrid, nextFirst.y, nextFirst.beatIndex);
    }
}
