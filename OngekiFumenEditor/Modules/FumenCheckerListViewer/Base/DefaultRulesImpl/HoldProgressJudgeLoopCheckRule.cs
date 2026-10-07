using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 长条判定点步长归零导致游戏死循环：
    /// 游戏侧 BPM.getProgressJudgeGrid() 以 resT/4 为起点反复二分，当 PROGJUDGE_BPM 远大于该段 BPM
    /// （或 TICKRESOLUTION 过小）时结果变成 0，HoldObj.CreateJudgePoint 的 while 循环不再推进 → 主线程卡死。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class HoldProgressJudgeLoopCheckRule : IFumenCheckRule
    {
        private const string RuleName = "HoldProgressJudgeLoop";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            // 游戏侧 Header.decode 会把 ≤1.00001 的 PROGJUDGE_BPM 直接夹成 240，这里按夹取后的值模拟。
            var progJudgeBpm = fumen.MetaInfo.ProgJudgeBpm;
            if (progJudgeBpm <= 1.00001)
                progJudgeBpm = 240;

            var resTime = fumen.MetaInfo.TRESOLUTION;

            foreach (var hold in fumen.Holds)
            {
                if (hold.HoldEnd is null)
                    continue;

                var offendingBpm = EnumerateEffectiveBpms(fumen, hold)
                    .FirstOrDefault(x => GetProgressJudgeGrid(x.BPM, progJudgeBpm, resTime) == 0);
                if (offendingBpm is null)
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.HoldProgressJudgeLoop.Format(hold.TGrid, offendingBpm.BPM, progJudgeBpm),
                    LocationDescription = hold.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(hold),
                };
            }
        }

        /// <summary>
        /// 长条区间内会生效的 BPM 段：起点处的生效值，加上区间内部（不含两端）的每个 BPM 变化。
        /// </summary>
        private static IEnumerable<BPMChange> EnumerateEffectiveBpms(OngekiFumen fumen, Hold hold)
        {
            var endTGrid = hold.HoldEnd.TGrid;
            var firstBpm = fumen.BpmList.FirstOrDefault();

            if (firstBpm is null)
                yield break;

            yield return hold.TGrid < firstBpm.TGrid ? firstBpm : fumen.BpmList.GetBpm(hold.TGrid);

            foreach (var bpmChange in fumen.BpmList.Where(x => hold.TGrid < x.TGrid && x.TGrid < endTGrid))
                yield return bpmChange;
        }

        /// <summary>
        /// 复刻游戏 BPM.getProgressJudgeGrid()：num2 从 resT/4 起步，PROGJUDGE_BPM 更大时反复二分；
        /// 返回值 0 表示判定点步长为 0（游戏侧死循环）。
        /// </summary>
        private static int GetProgressJudgeGrid(double bpm, double progJudgeBpm, int resTime)
        {
            var num = Math.Max(bpm, 3.75f);
            var num2 = resTime / 4;

            if (num < progJudgeBpm)
            {
                while (num < progJudgeBpm)
                {
                    num2 /= 2;
                    num *= 2;
                }
            }
            else
            {
                progJudgeBpm *= 2;
                while (num >= progJudgeBpm)
                {
                    num2 *= 2;
                    progJudgeBpm *= 2;
                }
            }

            return num2;
        }
    }
}
