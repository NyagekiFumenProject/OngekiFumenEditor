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
        public static TGrid ConvertYToTGrid(double pickY, FumenVisualEditorViewModel editor) => ConvertYToTGrid(pickY, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static TGrid ConvertYToTGrid(double pickY, BpmList bpmList, int tUnitLength = 240)
        {
            var positionBpmList = GetAllBpmUniformPositionList(bpmList, tUnitLength);

            //获取pickY对应的bpm和bpm起始位置
            (var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.startY <= pickY);
            if (pickBpm is null)
                return default;
            var relativeBpmLenOffset = pickBpm.LengthConvertToOffset(pickY - pickStartY, tUnitLength);

            var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
            return pickTGrid;
        }

        public static double ConvertTGridToY(TGrid tGrid, FumenVisualEditorViewModel editor) => ConvertTGridToY(tGrid, editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static double ConvertTGridToY(TGrid tGrid, BpmList bpmList, int tUnitLength = 240)
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

            var pickTGrid = pickStartY + relativeBpmLenOffset;
            return pickTGrid;
        }

        public static IEnumerable<(double startY, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor) => GetAllBpmUniformPositionList(editor.Fumen.BpmList, editor.Setting.TGridUnitLength);
        public static IEnumerable<(double startY, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList, int tUnitLength = 240) => bpmList.GetCachedAllBpmUniformPositionList(tUnitLength);
    }
}
