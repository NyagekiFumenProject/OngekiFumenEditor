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
    /// 局部 Soflan 区域（ISF）是「T 区间 × X 区间」构成的矩形。两块区域的交集只要有正的面积，
    /// 重叠部分就会同时落入两个区域的 soflan pattern，游戏侧实际生效的行为取决于处理顺序，属于谱面问题。
    /// 只共享一条边（首尾相接、左右并排）不算重叠。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class IndividualSoflanAreaOverlapCheckRule : IFumenCheckRule
    {
        private const string RuleName = "IndividualSoflanAreaOverlap";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            var areas = fumen.IndividualSoflanAreaMap.Values
                .SelectMany(x => x)
                .Select(AreaBounds.From)
                .OrderBy(x => x.TMin)
                .ToList();

            for (var i = 0; i < areas.Count; i++)
            {
                var a = areas[i];

                // 按起点排序后扫描：后一块的起点一旦不早于前一块的终点，后续区域也不可能再与之相交。
                for (var j = i + 1; j < areas.Count && areas[j].TMin < a.TMax; j++)
                {
                    var b = areas[j];

                    var tMin = Math.Max(a.TMin, b.TMin);
                    var tMax = Math.Min(a.TMax, b.TMax);
                    var xMin = Math.Max(a.XMin, b.XMin);
                    var xMax = Math.Min(a.XMax, b.XMax);

                    // 严格不相交才算安全：tMax == tMin 或 xMax == xMin 只是贴边，交集面积为零。
                    if (tMax <= tMin || xMax <= xMin)
                        continue;

                    yield return new CommonCheckResult
                    {
                        RuleName = RuleName,
                        Severity = RuleSeverity.Problem,
                        Description = Resources.IndividualSoflanAreaOverlap.Format(a.Area.SoflanGroup, b.Area.SoflanGroup),
                        LocationDescription = $"TGrid({tMin:0.###},{tMax:0.###}) XGrid({xMin:0.###},{xMax:0.###})",
                        // 指向较晚开始的一块：它通常就是后放/多出来的那一块，跳过去即可看到重叠现场。
                        NavigateBehavior = new NavigateToObjectBehavior(b.Area),
                    };
                }
            }
        }

        private readonly record struct AreaBounds(IndividualSoflanArea Area, double TMin, double TMax, double XMin, double XMax)
        {
            public static AreaBounds From(IndividualSoflanArea area) => new(
                area,
                Math.Min(area.TGrid.TotalUnit, area.EndIndicator.TGrid.TotalUnit),
                Math.Max(area.TGrid.TotalUnit, area.EndIndicator.TGrid.TotalUnit),
                Math.Min(area.XGrid.TotalUnit, area.EndIndicator.XGrid.TotalUnit),
                Math.Max(area.XGrid.TotalUnit, area.EndIndicator.XGrid.TotalUnit));
        }
    }
}
