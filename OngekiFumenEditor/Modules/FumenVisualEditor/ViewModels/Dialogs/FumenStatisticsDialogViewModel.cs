using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser.Ogkr;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Dialogs
{
    public class FumenStatisticsDialogViewModel : Screen
    {
        public sealed record StatisticsRow(string Name, int Count, string Description = "");

        public string FumenName { get; }
        public IReadOnlyList<StatisticsRow> ObjectCounts { get; }
        public IReadOnlyList<StatisticsRow> CommandCounts { get; } = Array.Empty<StatisticsRow>();
        public int TotalObjectCount => ObjectCounts.Sum(x => x.Count);
        public string CalculationError { get; } = string.Empty;
        public bool HasCalculationError => !string.IsNullOrEmpty(CalculationError);

        public FumenStatisticsDialogViewModel(OngekiFumen fumen, string fumenName)
        {
            DisplayName = Resources.FumenStatistics;
            FumenName = fumenName;

            // 直接枚举谱面集合，不使用可见区域或选择列表；不重复统计 HoldEnd 和自动生成的结束标记。
            var sideTaps = fumen.Taps.Count(x => x.IsWallTap);
            var sideHolds = fumen.Holds.Count(x => x.IsWallHold);
            ObjectCounts = new StatisticsRow[]
            {
                new(Resources.FumenStatisticsTap, fumen.Taps.Count - sideTaps),
                new(Resources.FumenStatisticsSideTap, sideTaps),
                new(Resources.FumenStatisticsHold, fumen.Holds.Count - sideHolds),
                new(Resources.FumenStatisticsSideHold, sideHolds),
                new("Flick", fumen.Flicks.Count),
                new("Bell", fumen.Bells.Count),
                new("Bullet", fumen.Bullets.Count),
                new(Resources.FumenStatisticsLaneStarts, fumen.Lanes.Count),
                new(Resources.FumenStatisticsLaneChildren, fumen.Lanes.Sum(x => x.ChildCount)),
                new(Resources.FumenStatisticsBeamStarts, fumen.Beams.Count),
                new(Resources.FumenStatisticsBeamChildren, fumen.Beams.Sum(x => x.ChildCount)),
                new(Resources.FumenStatisticsCurveControls, fumen.Lanes.SelectMany(x => x.Children)
                    .Concat(fumen.Beams.SelectMany(x => x.Children)).Sum(x => x.PathControls.Count)),
                new("BPM", fumen.BpmList.Count()),
                new(Resources.FumenStatisticsMeters, fumen.MeterChanges.Count()),
                new("Soflan", fumen.SoflansMap.Values.Sum(x => x.Count())),
                new(Resources.FumenStatisticsSoflanAreas, fumen.IndividualSoflanAreaMap.Values.Sum(x => x.Count())),
                new(Resources.FumenStatisticsLaneBlocks, fumen.LaneBlocks.Count),
                new("ClickSE", fumen.ClickSEs.Count),
                new("EnemySet", fumen.EnemySets.Count),
                new(Resources.FumenStatisticsComments, fumen.Comments.Count),
                new(Resources.FumenStatisticsSvgPrefabs, fumen.SvgPrefabs.Count),
                new(Resources.FumenStatisticsBulletPalletes, fumen.BulletPalleteList.Count),
            };

            try
            {
                // 与 OGKR 序列化共用计算器，包含 Hold 的持续判定数，而非仅统计 Hold 物件数。
                var statistics = FumenStatisticsCalculator.CalculateObjectStatisticsAsync(fumen);
                CommandCounts = new StatisticsRow[]
                {
                    new("T_TOTAL", statistics.TotalObjects, Resources.FumenStatisticsTotalDescription),
                    new("T_TAP", statistics.TapObjects, Resources.FumenStatisticsTapDescription),
                    new("T_HOLD", statistics.HoldObjects, Resources.FumenStatisticsHoldDescription),
                    new("T_SIDE", statistics.SideObjects, Resources.FumenStatisticsSideDescription),
                    new("T_SHOLD", statistics.SideHoldObjects, Resources.FumenStatisticsSideHoldDescription),
                    new("T_FLICK", statistics.FlickObjects, Resources.FumenStatisticsFlickDescription),
                    new("T_BELL", statistics.BellObjects, Resources.FumenStatisticsBellDescription),
                };
            }
            catch (Exception e) when (e is InvalidOperationException or OverflowException or ArgumentException)
            {
                CalculationError = string.Format(Resources.FumenStatisticsCalculationFailed, e.Message);
            }
        }

        public Task Close() => TryCloseAsync();
    }
}
