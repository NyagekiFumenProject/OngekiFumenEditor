using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using System;
using System.Linq;

namespace OngekiFumenEditor.Utils
{
    public static class TGridAudioTimeCalculator
    {
        public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
        {
            var positionBpmList = bpmList.GetCachedAllBpmUniformPositionList();
            var pick = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);

            if (pick.bpm is null)
            {
                if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
                    return TimeSpan.Zero;
                return default;
            }

            var relativeBpmLenOffset = TimeSpan.FromMilliseconds(BpmMathUtils.CalculateBPMLength(pick.bpm, tGrid));
            return pick.audioTime + relativeBpmLenOffset;
        }
    }
}
