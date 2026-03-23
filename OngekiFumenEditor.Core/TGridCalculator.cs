using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Modules.FumenVisualEditor
{
    public static class TGridCalculator
    {
        #region Frame -> AudioTime

        public const float FRAME_DURATION = 16.666666f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ConvertFrameToAudioTime(float frame)
           => TimeSpan.FromMilliseconds(FRAME_DURATION * frame);

        #endregion

        #region AudioTime -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList);

            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.audioTime <= audioTime);
            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((audioTime - pickStartY).TotalMilliseconds);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region TGrid -> AudioTime

        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList);

            (var audioTimeMsecBase, var pickBpm) = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
                    return TimeSpan.FromMilliseconds(0);
                else
                    return default;
            var relativeBpmLenOffset = TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(pickBpm, tGrid));

            var audioTimeMsec = audioTimeMsecBase + relativeBpmLenOffset;
            return audioTimeMsec;
        }

        #endregion

        #region [DesignMode] Y -> TGrid

        public static TGrid ConvertYToTGrid_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
        {
            pickY = pickY / scale;
            var list = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList);

            var pos = list.LastOrDefault(x => x.Y <= pickY);
            if (pos.Bpm is null)
                return default;
            var absSpeed = Math.Abs(pos.Speed);
            var relativeBpmLenOffset = pos.Bpm.LengthConvertToOffset((pickY - pos.Y) / absSpeed);

            var pickTGrid = pos.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region [DesignMode] Y -> AudioTime

        public static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var tGrid = ConvertYToTGrid_DesignMode(pickY, soflanList, bpmList, scale);
            if (tGrid is null)
                return default;
            return ConvertTGridToAudioTime(tGrid, bpmList);
        }

        #endregion

        #region [DesignMode] AudioTime -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridToY_DesignMode(ConvertAudioTimeToTGrid(audioTime, bpmList), soflanList, bpmList, scale);

        #endregion

        #region [DesignMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_DesignMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridUnitToY_DesignMode(tGrid.TotalUnit, soflanList, bpmList, scale);

        public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var positionBpmList = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList);

            var pos = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
            if (pos.Bpm is null)
                return default;

            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);

            var absSpeed = Math.Abs(pos.Speed);
            var y = (pos.Y + relativeBpmLenOffset * absSpeed) * scale;

            return y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, object scale)
            => ConvertTGridUnitToY_DesignMode(tGridUnit, soflanList, bpmList, Convert.ToDouble(scale));

        #endregion

        #region [PreviewMode] Y -> TGrid[]

        public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var r = soflanList.GetVisibleRanges_PreviewMode(pickY, 0, 0, bpmList, scale);
            var result = r.OrderBy(x => x.minTGrid).Select(x => x.minTGrid);
            return result;
        }

        #endregion

        #region [PreviewMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridUnitToY_PreviewMode(tGrid.TotalUnit, soflanList, bpmList, scale);

        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
        {
            var positionBpmList = soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList);

            var pos = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
            if (pos.Bpm is null)
                return default;

            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);
            var speed = pos.Speed;

            var y = (pos.Y + relativeBpmLenOffset * speed) * scale;
            return y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, SoflanList soflanList, BpmList bpmList, double scale)
            => ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, bpmList), soflanList, bpmList, scale);

        #endregion

        #region [PreviewMode] VisbleTimelines

        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double currentY, double viewHeight, double judgeLineOffsetY, int beatSplit, double scale)
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

        #endregion

        #region [DesignMode] VisbleTimelines

        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale)
        {
            minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);
            var minVisibleCanvasTGrid = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale);

            var endTGrid = ConvertYToTGrid_DesignMode(maxVisibleCanvasY, soflans, bpmList, scale);
            var currentTGridBaseOffset = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale)
                ?? ConvertYToTGrid_DesignMode(minVisibleCanvasY + judgeLineOffsetY, soflans, bpmList, 1);

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
            if (timeSignatures is null)
                yield break;

            var currentTimeSignatureIndex = timeSignatures.LastOrDefaultIndexByBinarySearch(minVisibleCanvasTGrid, x => x.startTGrid);
            (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

            if (endTGrid is null)
                yield break;

            while (currentTGridBaseOffset is not null)
            {
                var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
                var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

                (_, var currentTGridBase, var currentMeter, var currentBpm) = currentTimeSignature;
                (_, var nextTGridBase, _, var nextBpm) = nextTimeSignature;

                var resT = currentTGridBase.ResT;
                var beatCount = currentMeter.BunShi * beatSplit;
                var lengthCount = currentMeter.Bunbo * beatSplit;

                if (beatCount == 0 || lengthCount == 0)
                {
                    var y = ConvertTGridToY_DesignMode(currentTGridBase, soflans, bpmList, 1);
                    yield return (currentTGridBase, y * scale, 0, currentMeter, currentBpm);
                }
                else
                {
                    var lengthPerBeat = resT * 1.0d / lengthCount;
                    var diff = currentTGridBaseOffset - currentTGridBase;
                    var totalGrid = diff.Unit * resT + diff.Grid;
                    var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

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
                currentTimeSignature = nextTimeSignature;
            }
        }

        #endregion

        public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, BpmList bpmList, MeterChangeList meterList)
        {
            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
            var idx = timeSignatures.BinarySearchBy(tGrid, x => x.startTGrid);
            idx = idx < 0 ? Math.Max(0, ((~idx) - 1)) : idx;
            return timeSignatures[idx];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList)
            => bpmList.GetCachedAllBpmUniformPositionList();

        public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale)
        {
            var resT = bpm.TGrid.ResT;
            var beatCount = meter.BunShi * beatSplit;
            var lengthPerBeat = resT * 1.0d / beatCount;

            return MathUtils.CalculateBPMLength(bpm, bpm.TGrid + new GridOffset(0, (int)lengthPerBeat)) * scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime_DesignMode(float y, float range, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
        {
            var result = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y - range, y + range, 0, beatSplit, scale).MinByOrDefault(x => Math.Abs(x.y - y));
            return (result.tGrid, result.y, result.beatIndex);
        }

        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime_DesignMode(float y, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
        {
            var timeSignatures = meterChanges.GetCachedAllTimeSignatureUniformPositionList(bpmList);
            var tGrid = ConvertYToTGrid_DesignMode(y, soflans, bpmList, scale);
            if (tGrid is null)
                return default;
            var audioTime = ConvertTGridToAudioTime(tGrid, bpmList);

            (var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime <= audioTime);
            var prevTGrid = ConvertAudioTimeToTGrid(prevAudioTime, bpmList);
            var prevY = ConvertTGridToY_DesignMode(prevTGrid, soflans, bpmList, scale);

            var downFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, prevY, y, 0, beatSplit, scale)
                .LastOrDefault();
            var nextFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y, y + CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale), 0, beatSplit, scale)
                .FirstOrDefault();

            if (Math.Abs(downFirst.y - y) < Math.Abs(nextFirst.y - y))
                return (downFirst.tGrid, downFirst.y, downFirst.beatIndex);
            return (nextFirst.tGrid, nextFirst.y, nextFirst.beatIndex);
        }
    }
}
