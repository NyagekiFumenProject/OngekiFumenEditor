using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.Linq;

namespace OngekiFumenEditor.Parser.Ogkr
{
    //special thanks geeki___ for saving my hair. ^_^
    public class FumenStatisticsCalculator
    {
        public static FumenStatisticsResult CalculateObjectStatisticsAsync(OngekiFumen fumen)
        {
            var result = new FumenStatisticsResult();

            result.BellObjects = CalculateBellObjectsAsync(fumen);
            result.FlickObjects = CalculateFlickObjectsAsync(fumen);
            result.SideObjects = CalculateSideObjectsAsync(fumen);
            result.TapObjects = CalculateTapObjectsAsync(fumen);
            result.SideHoldObjects = CalculateSideHoldObjectsAsync(fumen);
            result.HoldObjects = CalculateHoldObjectsAsync(fumen);

            result.TotalObjects =
                result.FlickObjects +
                result.SideObjects +
                result.HoldObjects +
                result.TapObjects +
                result.SideHoldObjects;

            // 白金分数上限：每个判定单元最多 2 分（游戏 Counters.addPlatinumScore 每次 +2，
            // 上限 _tsjNoteTotal / volumeJudgeTechMax * 2；注 volumeNotesTech 全 1 -> TotalObjects × 2）。
            result.PlatinumScoreMax = result.TotalObjects * 2;

            // HoldTick 贡献：持续判定点（judgeList 点数，含终点、不含起点），每个判定单元权重相同。
            result.HoldTickObjects = result.HoldObjects + result.SideHoldObjects;
            result.HoldTickShare = result.TotalObjects > 0
                ? (double)result.HoldTickObjects / result.TotalObjects
                : 0;
            /*
            var msg = $"T_TOTAL {result.TotalObjects}{Environment.NewLine}";
            msg += $"T_TAP {result.TapObjects}{Environment.NewLine}";
            msg += $"T_HOLD {result.HoldObjects}{Environment.NewLine}";
            msg += $"T_SIDE {result.SideObjects}{Environment.NewLine}";
            msg += $"T_SHOLD {result.SideHoldObjects}{Environment.NewLine}";
            msg += $"T_FLICK {result.FlickObjects}{Environment.NewLine}";
            msg += $"T_BELL {result.BellObjects}{Environment.NewLine}";

            MessageBox.Show(msg);
            */
            return result;
        }

        private static int CalculateHoldObjectsAsync(OngekiFumen fumen)
        {
            return fumen.Holds
                .Where(x => x.ReferenceLaneStart?.IsWallLane != true)
                .Select(x => CalculateHold(x, fumen))
                .Sum();
        }

        private static int CalculateSideHoldObjectsAsync(OngekiFumen fumen)
        {
            return fumen.Holds
                .Where(x => x.ReferenceLaneStart?.IsWallLane == true)
                .Select(x => CalculateHold(x, fumen))
                .Sum();
        }

        private static int CalculateHold(Hold x, OngekiFumen fumen)
        {
            // 与游戏判定点（MU3 Reader/HoldObj.CreateJudgePoint）保持一致：
            // 步长统一走 HoldTickStepCalculator（含 BPM 3.75 下限与 float32 语义，基准为本编辑器网格的 1/4 切片），
            // 起点单独计为 Tap、这里不计入；终点判定固定计入一次。
            var progressJudgeBpm = fumen.MetaInfo.ProgJudgeBpm;

            var holdStartTGrid = x.TGrid;
            var holdEndTGrid = x.HoldEnd?.TGrid;

            var count = 0;
            var tickGrid = HoldTickStepCalculator.Calculate(fumen.BpmList.GetBpm(holdStartTGrid).BPM, progressJudgeBpm, HoldTickStepCalculator.DefaultStandardBeatLen);
            var curTGrid = holdStartTGrid + new GridOffset(0, tickGrid);

            if (holdEndTGrid is not null)
            {
                while (curTGrid < holdEndTGrid)
                {
                    count++;
                    tickGrid = HoldTickStepCalculator.Calculate(fumen.BpmList.GetBpm(curTGrid).BPM, progressJudgeBpm, HoldTickStepCalculator.DefaultStandardBeatLen);
                    curTGrid = curTGrid + new GridOffset(0, tickGrid);
                }
            }

            return count + 1;
        }

        private static int CalculateTapObjectsAsync(OngekiFumen fumen)
        {
            return fumen.Taps.Count() + fumen.Holds.Count() - CalculateSideObjectsAsync(fumen);
        }

        private static int CalculateSideObjectsAsync(OngekiFumen fumen)
        {
            var wallTapsCount = fumen.Taps.Where(x => x.IsWallTap).Count();
            var wallHoldCount = fumen.Holds.Where(x => x.IsWallHold).Count();
            return wallTapsCount + wallHoldCount;
        }

        private static int CalculateFlickObjectsAsync(OngekiFumen fumen)
        {
            return fumen.Flicks.Count;
        }

        private static int CalculateBellObjectsAsync(OngekiFumen fumen)
        {
            return fumen.Bells.Count;
        }
    }
}
