using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// BPM 记录取值过小（≤0 或 &lt;0.0001）：
    /// 游戏侧 BPM.decode 会把 ≤0 夹成 1e-6，而 BPMList.calcTGrid 对 bpm &lt; 0.0001 直接跳过
    /// 该段的时间轴换算 —— 之后物件的 msec 保持无效哨兵值，时间轴/判定/渲染全部错乱。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class BpmOutOfRangeCheckRule : IFumenCheckRule
    {
        private const string RuleName = "BpmOutOfRange";

        /// <summary>游戏侧 BPMList.calcTGrid 的跳过阈值。</summary>
        private const double MinGameBpm = 0.0001;

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var bpmChange in fumen.BpmList)
            {
                if (bpmChange.BPM >= MinGameBpm)
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.BpmOutOfRange.Format(bpmChange.TGrid, bpmChange.BPM),
                    LocationDescription = bpmChange.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(bpmChange),
                };
            }
        }
    }
}
