using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class TGridCalculator
    {
        #region [DesignMode] Y -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertYToTGrid_DesignMode(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToTGrid_DesignMode(pickY, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        private static TGrid ConvertYToTGrid_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
        {
            pickY = pickY / scale;
            var list = soflanList.GetCachedSoflanPositionList_DesignMode(tUnitLength, bpmList);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var tGrid, var speed, var pickBpm) = list.LastOrDefault(x => x.startY <= pickY);
            if (pickBpm is null)
                return default;
            var absSpeed = Math.Abs(speed);
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((pickY - pickStartY) / absSpeed, tUnitLength);

            var pickTGrid = tGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region [DesignMode] AudioTime -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_DesignMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

        #endregion

        #region AudioTime -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, FumenVisualEditorViewModel editor)
           => ConvertAudioTimeToTGrid(audioTime, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList, int tUnitLength = 240)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList, tUnitLength);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.audioTime <= audioTime);
            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((audioTime - pickStartY).TotalMilliseconds, tUnitLength);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region [DesignMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_DesignMode(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_DesignMode(tGrid, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_DesignMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
            => ConvertTGridUnitToY_DesignMode(tGrid.TotalUnit, soflanList, bpmList, scale, tUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, FumenVisualEditorViewModel editor)
            => ConvertTGridUnitToY_DesignMode(tGridUnit, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
        {
            var positionBpmList = soflanList.GetCachedSoflanPositionList_DesignMode(tUnitLength, bpmList);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var tGrid, var speed, var pickBpm) = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.startTGrid.TotalUnit);

            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().bpmChange?.TGrid is TGrid first && tGridUnit < first.TotalUnit)
                    return 0;
                else
                    return default;

            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(tGrid.TotalUnit, tGridUnit, pickBpm.BPM, tUnitLength);

            var absSpeed = Math.Abs(speed);
            var y = (pickStartY + relativeBpmLenOffset * absSpeed) * scale;

            return y;
        }

        #endregion

        #region TGrid -> AudioTime

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToAudioTime(tGrid, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList, int tUnitLength = 240)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList, tUnitLength);

            //获取pickY对应的bpm和bpm起始位置
            (var audioTimeMsecBase, var pickBpm) = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
                    return TimeSpan.FromMilliseconds(0);
                else
                    return default;
            var relativeBpmLenOffset = TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(pickBpm, tGrid, tUnitLength));

            var audioTimeMsec = audioTimeMsecBase + relativeBpmLenOffset;
            return audioTimeMsec;
        }

        #endregion

        #region [PreviewMode] Y -> TGrid

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TGrid ConvertYToTGrid_PreviewMode(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToTGrid_PreviewMode(pickY, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        private static TGrid ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
        {
            pickY = pickY / scale;
            var list = soflanList.GetCachedSoflanPositionList_PreviewMode(tUnitLength, bpmList);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var tGrid, var speed, var pickBpm) = list.LastOrDefault(x => x.startY <= pickY);
            if (pickBpm is null)
                return default;
            var absSpeed = Math.Abs(speed);
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((pickY - pickStartY) / absSpeed, tUnitLength);

            var pickTGrid = tGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        #endregion

        #region [PreviewMode] TGrid -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridToY_PreviewMode(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_PreviewMode(tGrid, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
            => ConvertTGridUnitToY_PreviewMode(tGrid.TotalUnit, soflanList, bpmList, scale, tUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, FumenVisualEditorViewModel editor)
            => ConvertTGridUnitToY_PreviewMode(tGridUnit, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale, int tUnitLength)
        {
            var positionBpmList = soflanList.GetCachedSoflanPositionList_PreviewMode(tUnitLength, bpmList);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var tGrid, var speed, var pickBpm) = positionBpmList.LastOrDefault(x => x.startTGrid.TotalUnit <= tGridUnit);
            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().bpmChange?.TGrid is TGrid first && tGridUnit < first.TotalUnit)
                    return 0;
                else
                    return default;

            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(tGrid.TotalUnit, tGridUnit, pickBpm.BPM, tUnitLength);

            var absSpeed = Math.Abs(speed);
            var y = (pickStartY + relativeBpmLenOffset * absSpeed) * scale;
            return y;
        }

        #endregion

        #region [PreviewMode] AudioTime -> Y

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
            => ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

        #endregion

        #region [PreviewMode] VisbleTimelines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => GetVisbleTimelines_PreviewMode(editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Rect.MinY, editor.Rect.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale, int tUnitLength = 240)
        {
            minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);
            var minVisibleCanvasTGrid = ConvertYToTGrid_PreviewMode(minVisibleCanvasY, soflans, bpmList, scale, tUnitLength);

            //划线的中止位置
            var endTGrid = ConvertYToTGrid_PreviewMode(maxVisibleCanvasY, soflans, bpmList, scale, tUnitLength);
            //可显示划线的起始位置 
            var currentTGridBaseOffset = ConvertYToTGrid_PreviewMode(minVisibleCanvasY, soflans, bpmList, scale, tUnitLength)
                ?? ConvertYToTGrid_PreviewMode(minVisibleCanvasY + judgeLineOffsetY, soflans, bpmList, 1, tUnitLength);

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
            var currentTimeSignatureIndex = 0;
            //快速定位,尽量避免计算完全不用画的timesignature(
            for (int i = 0; i < timeSignatures.Count; i++)
            {
                var cur = timeSignatures[i];
                if (cur.startTGrid > minVisibleCanvasTGrid)
                    break;
                currentTimeSignatureIndex = i;
            }

            //钦定好要画的起始timeSignatrue
            (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

            if (endTGrid is null)
                yield break;

            while (currentTGridBaseOffset is not null)
            {
                var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
                var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

                //钦定好要画的相对于当前timeSignature的偏移Y，节拍信息，节奏速度
                (_, var currentTGridBase, var currentMeter, var currentBpm) = currentTimeSignature;
                var currentStartY = ConvertTGridToY_PreviewMode(currentTGridBase, soflans, bpmList, scale, tUnitLength);
                (_, var nextTGridBase, _, var nextBpm) = nextTimeSignature;

                //计算每一拍的(grid)长度
                var resT = currentTGridBase.ResT;
                var beatCount = currentMeter.BunShi * beatSplit;
                var lengthPerBeat = resT * 1.0d / beatCount;

                //这里也可以跳过添加完全看不到的线
                var diff = currentTGridBaseOffset - currentTGridBase;
                var totalGrid = diff.Unit * resT + diff.Grid;
                var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

                //检测是否可以绘制线
                var isDrawable = !(double.IsInfinity(lengthPerBeat) || (beatCount == 0));

                while (isDrawable)
                {
                    var tGrid = currentTGridBase + new GridOffset(0, (int)(lengthPerBeat * i));
                    //因为是不存在跨bpm长度计算，可以直接CalculateBPMLength(...)计算而不是TGridCalculator.ConvertTGridToY(...);
                    var y = ConvertTGridToY_PreviewMode(tGrid, soflans, bpmList, 1, tUnitLength);
                    //var y = currentStartY + len;

                    //超过当前timeSignature范围，切换到下一个timeSignature画新的线
                    if (nextBpm is not null && tGrid >= nextTGridBase)
                        break;
                    //超过编辑器谱面范围，后面都不用画了
                    if (tGrid > endTGrid)
                        yield break;
                    //节奏线在最低可见线的后面
                    if (tGrid < currentTGridBaseOffset)
                    {
                        i++;
                        continue;
                    }

                    yield return (tGrid, y * scale, i % beatCount, currentMeter, currentBpm);
                    i++;
                }
                currentTGridBaseOffset = nextTGridBase;
                currentTimeSignatureIndex = nextTimeSignatureIndex;
                currentTimeSignature = timeSignatures.Count > currentTimeSignatureIndex ? timeSignatures[currentTimeSignatureIndex] : default;
            }
        }


        #endregion

        #region [DesignMode] VisbleTimelines

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => GetVisbleTimelines_DesignMode(editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Rect.MinY, editor.Rect.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale, int tUnitLength = 240)
        {
            minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);
            var minVisibleCanvasTGrid = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale, tUnitLength);

            //划线的中止位置
            var endTGrid = ConvertYToTGrid_DesignMode(maxVisibleCanvasY, soflans, bpmList, scale, tUnitLength);
            //可显示划线的起始位置 
            var currentTGridBaseOffset = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale, tUnitLength)
                ?? ConvertYToTGrid_DesignMode(minVisibleCanvasY + judgeLineOffsetY, soflans, bpmList, 1, tUnitLength);

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
            var currentTimeSignatureIndex = 0;
            //快速定位,尽量避免计算完全不用画的timesignature(
            for (int i = 0; i < timeSignatures.Count; i++)
            {
                var cur = timeSignatures[i];
                if (cur.startTGrid > minVisibleCanvasTGrid)
                    break;
                currentTimeSignatureIndex = i;
            }

            //钦定好要画的起始timeSignatrue
            (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

            if (endTGrid is null)
                yield break;

            while (currentTGridBaseOffset is not null)
            {
                var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
                var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

                //钦定好要画的相对于当前timeSignature的偏移Y，节拍信息，节奏速度
                (_, var currentTGridBase, var currentMeter, var currentBpm) = currentTimeSignature;
                var currentStartY = ConvertTGridToY_DesignMode(currentTGridBase, soflans, bpmList, scale, tUnitLength);
                (_, var nextTGridBase, _, var nextBpm) = nextTimeSignature;

                //计算每一拍的(grid)长度
                var resT = currentTGridBase.ResT;
                var beatCount = currentMeter.BunShi * beatSplit;
                var lengthPerBeat = resT * 1.0d / beatCount;

                //这里也可以跳过添加完全看不到的线
                var diff = currentTGridBaseOffset - currentTGridBase;
                var totalGrid = diff.Unit * resT + diff.Grid;
                var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

                //检测是否可以绘制线
                var isDrawable = !(double.IsInfinity(lengthPerBeat) || (beatCount == 0));

                while (isDrawable)
                {
                    var tGrid = currentTGridBase + new GridOffset(0, (int)(lengthPerBeat * i));
                    //因为是不存在跨bpm长度计算，可以直接CalculateBPMLength(...)计算而不是TGridCalculator.ConvertTGridToY(...);
                    var y = ConvertTGridToY_DesignMode(tGrid, soflans, bpmList, 1, tUnitLength);
                    //var y = currentStartY + len;

                    //超过当前timeSignature范围，切换到下一个timeSignature画新的线
                    if (nextBpm is not null && tGrid >= nextTGridBase)
                        break;
                    //超过编辑器谱面范围，后面都不用画了
                    if (tGrid > endTGrid)
                        yield break;
                    //节奏线在最低可见线的后面
                    if (tGrid < currentTGridBaseOffset)
                    {
                        i++;
                        continue;
                    }

                    yield return (tGrid, y * scale, i % beatCount, currentMeter, currentBpm);
                    i++;
                }
                currentTGridBaseOffset = nextTGridBase;
                currentTimeSignatureIndex = nextTimeSignatureIndex;
                currentTimeSignature = timeSignatures.Count > currentTimeSignatureIndex ? timeSignatures[currentTimeSignatureIndex] : default;
            }
        }

        public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, BpmList bpmList, MeterChangeList meterList, int tUnitLength = 240)
        {
            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
            var idx = timeSignatures.BinarySearchBy(tGrid, x => x.startTGrid);
            idx = idx < 0 ? Math.Max(0, ((~idx) - 1)) : idx;
            return timeSignatures[idx];
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor)
            => GetAllBpmUniformPositionList(editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList, int tUnitLength = 240)
            => bpmList.GetCachedAllBpmUniformPositionList(tUnitLength);

        public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale, int tUnitLength = 240)
        {
            //计算每一拍的(grid)长度
            var resT = bpm.TGrid.ResT;
            var beatCount = meter.BunShi * beatSplit;
            var lengthPerBeat = (resT * 1.0d / beatCount);

            return MathUtils.CalculateBPMLength(bpm, bpm.TGrid + new GridOffset(0, (int)lengthPerBeat), tUnitLength) * scale;
        }

        /// <summary>
        /// 计算在y±range内，最近的节奏线
        /// </summary>
        /// <param name="y"></param>
        /// <param name="range"></param>
        /// <param name="bpmList"></param>
        /// <param name="meterChanges"></param>
        /// <param name="beatSplit"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime_DesignMode(float y, float range, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale, int tUnitLength = 240)
        {
            var result = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y - range, y + range, 0, beatSplit, scale, tUnitLength).MinByOrDefault(x => Math.Abs(x.y - y));
            return (result.tGrid, result.y, result.beatIndex);
        }
        /// <summary>
        /// 计算在y±range内，最近的节奏线
        /// </summary>
        /// <param name="y"></param>
        /// <param name="range"></param>
        /// <param name="editor"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => TryPickMagneticBeatTime_DesignMode(y, range, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => TryPickClosestBeatTime_DesignMode(y, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);

        /// <summary>
        /// 获取某个时间点上最近的节奏点
        /// </summary>
        /// <param name="y"></param>
        /// <param name="editor"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime_DesignMode(float y, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale, int tUnitLength = 240)
        {
            /**
             ...
              |
              |
            __|__ 
              |          downFirst
              |
            ------ prevY
             */
            var timeSignatures = meterChanges.GetCachedAllTimeSignatureUniformPositionList(tUnitLength, bpmList);
            //var tGrid = ConvertAudioTimeToTGrid(audioTime, bpmList, tUnitLength);
            //var y = ConvertTGridToY_DesignMode(tGrid, soflans, bpmList, scale, tUnitLength);
            var tGrid = ConvertYToTGrid_DesignMode(y, soflans, bpmList, scale, tUnitLength);
            if (tGrid is null)
                return default;
            var audioTime = ConvertTGridToAudioTime(tGrid, bpmList);

            (var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime <= audioTime);
            var prevTGrid = ConvertAudioTimeToTGrid(prevAudioTime, bpmList, tUnitLength);
            var prevY = ConvertTGridToY_DesignMode(prevTGrid, soflans, bpmList, scale, tUnitLength);

            var downFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, prevY, y, 0, beatSplit, scale, tUnitLength)
                .LastOrDefault();
            var nextFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y, y + CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale, tUnitLength), 0, beatSplit, scale, tUnitLength)
                .FirstOrDefault();

            if (Math.Abs(downFirst.y - y) < Math.Abs(nextFirst.y - y))
                return (downFirst.tGrid, downFirst.y, downFirst.beatIndex);
            return (nextFirst.tGrid, nextFirst.y, nextFirst.beatIndex);
        }
    }
}
