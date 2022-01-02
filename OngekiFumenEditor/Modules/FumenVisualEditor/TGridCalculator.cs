using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
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
        {
            var bpmList = editor.Fumen.BpmList;
            var setting = editor.Setting;
            var baseBPM = bpmList.GetBpm(setting.CurrentDisplayTimePosition);
            var positionBpmList = GetVisibleBpmList(editor).ToList();

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.startY <= pickY);
            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset(pickY - pickStartY, setting.BaseLineY);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        public static IEnumerable<(double startY, BPMChange bpm)> GetVisibleBpmList(FumenVisualEditorViewModel editor)
        {
            if (editor?.Fumen?.BpmList is null)
                yield break;
            var setting = editor.Setting;
            var bpmList = editor.Fumen.BpmList;
            var offsetY = setting.BaseLineY;
            var baseBPM = bpmList.GetBpm(setting.CurrentDisplayTimePosition);
            var baseOffsetLen = MathUtils.CalculateBPMLength(baseBPM, setting.CurrentDisplayTimePosition, setting.BaseLineY);

            if (baseOffsetLen < offsetY)
            {
                //表示可能还需要上一个BPM(如果有的话)参与计算，因为y可能会对应到上一个BPM（即物件可能在基轴下方
                var prevBPM = bpmList.GetPrevBpm(baseBPM);
                if (prevBPM is BPMChange)
                {
                    var bpmLen = MathUtils.CalculateBPMLength(prevBPM, baseBPM, setting.BaseLineY);
                    yield return (-baseOffsetLen - bpmLen + offsetY, prevBPM);
                }
            }

            var y = offsetY - baseOffsetLen;
            var curBpm = baseBPM;
            while (y <= editor.CanvasHeight)
            {
                yield return (y, curBpm);
                var nextBpm = bpmList.GetNextBpm(curBpm);
                if (nextBpm is null)
                    break;
                var bpmLen = MathUtils.CalculateBPMLength(curBpm, nextBpm, setting.BaseLineY);
                y += bpmLen;
                curBpm = nextBpm;
            }
        }

        public static double? ConvertTGridToY(TGrid tGrid, FumenVisualEditorViewModel editor)
        {
            var setting = editor.Setting;
            var bpmList = editor.Fumen.BpmList;
            var baseBPM = bpmList.GetBpm(setting.CurrentDisplayTimePosition);
            var positionBpmList = GetVisibleBpmList(editor).ToList();


            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
            if (pickBpm is null)
                if (tGrid < positionBpmList.FirstOrDefault().bpm?.TGrid)
                    return 0;
                else
                    return default;
            var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pickBpm, tGrid, setting.BaseLineY);

            var pickTGrid = pickStartY + relativeBpmLenOffset;
            return pickTGrid;
        }
    }
}
