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
    /// 局部 Soflan 区域（ISF）引用了不存在的 soflan 组，但区域内当前没有长条。
    /// 此时读谱不会崩，属于潜在隐患：以后只要有一个 Hold 落入该区域，游戏就会把区域 pattern
    /// 写进轨道并在 lane.format() 里查 soflanList[pattern] 抛 KeyNotFoundException。
    /// 已经包含长条的同一情况由 SoflanPatternMissingForArea 以 error 报告，这里跳过以免重复。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class SoflanPatternMissingForIdleAreaCheckRule : IFumenCheckRule
    {
        private const string RuleName = "SoflanPatternMissingForIdleArea";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var area in fumen.IndividualSoflanAreaMap.Values.SelectMany(x => x))
            {
                var group = area.SoflanGroup;

                // 组 0 在游戏侧的 soflan 表里恒存在（不依赖任何 SFL 记录）。
                if (group == SoflanListMap.DefaultSoflanGroup || fumen.SoflansMap.ContainsKey(group))
                    continue;

                // 有长条落入时已经是确定会崩的谱面，交给 SoflanPatternMissingForArea 报 error。
                if (IndividualSoflanAreaCheckUtils.ContainsHold(area, fumen))
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Problem,
                    Description = Resources.SoflanPatternMissingForIdleArea.Format(group),
                    LocationDescription = area.ToString(),
                    NavigateBehavior = new NavigateToObjectBehavior(area),
                };
            }
        }
    }
}
