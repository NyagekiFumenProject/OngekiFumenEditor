using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 局部 Soflan 区域（ISF）引用了不存在的 soflan 组、且区域内有长条：
    /// 游戏侧只有落在区域内的 Hold 会把区域 pattern 写进所引用轨道的 pattern，随后 lane.format()
    /// 直接查 soflanList[pattern]，缺键即抛 KeyNotFoundException（读谱崩溃）。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class SoflanPatternMissingForAreaCheckRule : IFumenCheckRule
    {
        private const string RuleName = "SoflanPatternMissingForArea";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var area in fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x))
            {
                var group = area.SoflanGroup;

                // 组 0 在游戏侧的 soflan 表里恒存在（不依赖任何 SFL 记录）。
                if (group == SoflanListMap.DefaultSoflanGroup || fumen.SoflansMap.ContainsKey(group))
                    continue;

                var hold = fumen.Holds.Cast<ILaneDockable>().FirstOrDefault(x => IndividualSoflanAreaCheckUtils.IsInsideArea(area, x));
                if (hold is null)
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.SoflanPatternMissingForArea.Format(group, hold.TGrid),
                    LocationDescription = area.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(area),
                };
            }
        }
    }
}
