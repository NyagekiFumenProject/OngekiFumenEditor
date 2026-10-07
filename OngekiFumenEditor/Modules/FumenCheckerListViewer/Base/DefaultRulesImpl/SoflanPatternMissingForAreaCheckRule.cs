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

                var hold = fumen.Holds.Cast<ILaneDockable>().FirstOrDefault(x => IsInsideArea(area, x));
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

        /// <summary>
        /// 与游戏侧 Notes.CheckSoflanPatternLange 对 Hold 的判定保持一致：
        /// T 区间为 [区域起点, 区域终点)，X 用整数 place（编辑器里就是 XGrid.TotalUnit 的整数部分）比较。
        /// </summary>
        private static bool IsInsideArea(IndividualSoflanArea area, ILaneDockable dockable)
        {
            if (dockable.TGrid < area.TGrid || dockable.TGrid >= area.EndIndicator.TGrid)
                return false;

            var x = (int)dockable.XGrid.TotalUnit;
            return x >= (int)area.XGrid.TotalUnit && x <= (int)area.EndIndicator.XGrid.TotalUnit;
        }
    }
}
