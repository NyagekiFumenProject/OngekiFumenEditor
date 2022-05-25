using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
    public static class TGridCalculator
    {
        public static TGrid ConvertYToTGrid(double pickY, FumenVisualEditorViewModel editor)
            => ConvertYToTGrid(pickY, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        public static TGrid ConvertYToTGrid(double pickY, BpmList bpmList, double scale, int tUnitLength)
        {
            pickY = pickY / scale;
            var positionBpmList = GetAllBpmUniformPositionList(bpmList, tUnitLength);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.startY <= pickY);
            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset(pickY - pickStartY, tUnitLength);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        public static double ConvertAudioTimeToY(TimeSpan audioTime, FumenVisualEditorViewModel editor)
            => ConvertTGridToY(ConvertAudioTimeToTGrid(audioTime, editor), editor);

        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, FumenVisualEditorViewModel editor) => ConvertAudioTimeToTGrid(audioTime, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList, int tUnitLength = 240)
            => ConvertYToTGrid(audioTime.TotalMilliseconds, bpmList, 1, tUnitLength);

        public static double ConvertTGridToY(TGrid tGrid, FumenVisualEditorViewModel editor)
            => ConvertTGridToY(tGrid, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale, editor.Setting.TGridUnitLength);
        public static double ConvertTGridToY(TGrid tGrid, BpmList bpmList, double scale, int tUnitLength)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList, tUnitLength);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
            if (pickBpm is null)
                if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
                    return 0;
                else
                    return default;
            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pickBpm, tGrid, tUnitLength);

            var pickTGrid = (pickStartY + relativeBpmLenOffset) * scale;
            return pickTGrid;
        }

        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, FumenVisualEditorViewModel editor) => ConvertTGridToAudioTime(tGrid, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
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
            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pickBpm, tGrid, tUnitLength);

            var audioTimeMsec = audioTimeMsecBase + relativeBpmLenOffset;
            return TimeSpan.FromMilliseconds(audioTimeMsec);
        }

        public static List<(double startY, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor) => GetAllBpmUniformPositionList(editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static List<(double startY, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList, int tUnitLength = 240) => bpmList.GetCachedAllBpmUniformPositionList(tUnitLength);

        public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale, int tUnitLength = 240)
        {
            //计算每一拍的(grid)长度
            var resT = bpm.TGrid.ResT;
            var beatCount = meter.BunShi * beatSplit;
            var lengthPerBeat = (resT * 1.0d / beatCount);

            return MathUtils.CalculateBPMLength(bpm, bpm.TGrid + new GridOffset(0, (int)lengthPerBeat), tUnitLength) * scale;
        }

        public static IEnumerable<(TGrid tGrid, double y, int beatIndex)> GetVisbleTimelines(FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => GetVisbleTimelines(editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.MinVisibleCanvasY, editor.MaxVisibleCanvasY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);
        public static IEnumerable<(TGrid tGrid, double y, int beatIndex)> GetVisbleTimelines(BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale, int tUnitLength = 240)
        {
            minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);

            //划线的中止位置
            var endTGrid = ConvertYToTGrid(maxVisibleCanvasY, bpmList, scale, tUnitLength);
            //可显示划线的起始位置
            var currentTGridBaseOffset = ConvertYToTGrid(minVisibleCanvasY, bpmList, scale, tUnitLength) ?? ConvertYToTGrid(minVisibleCanvasY + judgeLineOffsetY, bpmList, 1, tUnitLength);

            var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(240, bpmList);
            var currentTimeSignatureIndex = 0;
            //快速定位,尽量避免计算完全不用画的timesignature(
            for (int i = 0; i < timeSignatures.Count; i++)
            {
                var cur = timeSignatures[i];
                if (cur.startY > minVisibleCanvasY)
                    break;
                currentTimeSignatureIndex = i;
            }

            //钦定好要画的起始timeSignatrue
            (double startY, TGrid startTGrid, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

            if (endTGrid is null)
                yield break;

            while (currentTGridBaseOffset is not null)
            {
                var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
                var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

                //钦定好要画的相对于当前timeSignature的偏移Y，节拍信息，节奏速度
                (var currentStartY, var currentTGridBase, var currentMeter, var currentBpm) = currentTimeSignature;
                (var nextStartY, var nextTGridBase, _, var nextBpm) = nextTimeSignature;

                //计算每一拍的(grid)长度
                var resT = currentTGridBase.ResT;
                var beatCount = currentMeter.BunShi * beatSplit;
                var lengthPerBeat = resT * 1.0d / beatCount;

                //这里也可以跳过添加完全看不到的线
                var diff = currentTGridBaseOffset - currentTGridBase;
                var totalGrid = diff.Unit * resT + diff.Grid;
                var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

                while (true)
                {
                    var tGrid = currentTGridBase + new GridOffset(0, (int)(lengthPerBeat * i));
                    //因为是不存在跨bpm长度计算，可以直接CalculateBPMLength(...)计算而不是TGridCalculator.ConvertTGridToY(...);
                    var y = currentStartY + MathUtils.CalculateBPMLength(currentTGridBase, tGrid, currentBpm.BPM, 240);

                    //节奏线画不了惹,跳过
                    if (beatCount == 0)
                        break;
                    //超过当前timeSignature范围，切换到下一个timeSignature画新的线
                    if (nextBpm is not null && y >= nextStartY)
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

                    yield return (tGrid, y * scale, i % beatCount);
                    i++;
                }
                currentTGridBaseOffset = nextTGridBase;
                currentTimeSignatureIndex = nextTimeSignatureIndex;
                currentTimeSignature = timeSignatures.Count > currentTimeSignatureIndex ? timeSignatures[currentTimeSignatureIndex] : default;
            }
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
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale, int tUnitLength = 240)
            => GetVisbleTimelines(bpmList, meterChanges, y - range, y + range, 0, beatSplit, scale, tUnitLength).MinBy(x => Math.Abs(x.y - y));
        /// <summary>
        /// 计算在y±range内，最近的节奏线
        /// </summary>
        /// <param name="y"></param>
        /// <param name="range"></param>
        /// <param name="editor"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => TryPickMagneticBeatTime(y, range, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);

        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, FumenVisualEditorViewModel editor, int tUnitLength = 240)
            => TryPickClosestBeatTime(y, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale, tUnitLength);
        /// <summary>
        /// 获取某个时间点上最近的节奏点
        /// </summary>
        /// <param name="y"></param>
        /// <param name="editor"></param>
        /// <param name="tUnitLength"></param>
        /// <returns></returns>
        public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale, int tUnitLength = 240)
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
            var timeSignatures = meterChanges.GetCachedAllTimeSignatureUniformPositionList(240, bpmList);

            (var prevY, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.startY <= y);
            var downFirst = GetVisbleTimelines(bpmList, meterChanges, prevY, y, 0, beatSplit, scale, tUnitLength)
                .LastOrDefault();
            var nextFirst = GetVisbleTimelines(bpmList, meterChanges, y, y + CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale, tUnitLength), 0, beatSplit, scale, tUnitLength)
                .FirstOrDefault();

            if (Math.Abs(downFirst.y - y) < Math.Abs(nextFirst.y - y))
                return downFirst;
            return nextFirst;
        }
    }
}
