using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 默认变速组（<see cref="SoflanListMap.DefaultSoflanGroup"/>，即没有局部区域覆盖时的组 0）
    /// 的最后一条变速不是正速（Speed ≤ 0）时报警告：
    /// 变速是游戏侧时间轴映射的倍率，0 或负倍率会让映射停滞/倒退，物件时刻与判定随之错乱。
    /// （既有的 <c>Soflan</c> 规则检查「组末尾累积倍率是否回到 1x」，本规则专门盯默认组最后一条变速的取值。）
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class DefaultSoflanLastSpeedNonPositiveCheckRule : IFumenCheckRule
    {
        private const string RuleName = "DefaultSoflanLastSpeedNonPositive";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            if (!fumen.SoflansMap.TryGetValue(SoflanListMap.DefaultSoflanGroup, out var soflans))
                yield break;

            var lastSoflan = soflans.OrderBy(x => x.TGrid).LastOrDefault();
            if (lastSoflan is null || lastSoflan.Speed > 0)
                yield break;

            yield return new CommonCheckResult
            {
                RuleName = RuleName,
                Severity = RuleSeverity.Problem,
                Description = Resources.DefaultSoflanLastSpeedNonPositive.Format(lastSoflan.Speed, lastSoflan.TGrid),
                LocationDescription = lastSoflan.ToString(),
                NavigateBehavior = new NavigateToObjectBehavior(lastSoflan as OngekiTimelineObjectBase),
            };
        }
    }
}
