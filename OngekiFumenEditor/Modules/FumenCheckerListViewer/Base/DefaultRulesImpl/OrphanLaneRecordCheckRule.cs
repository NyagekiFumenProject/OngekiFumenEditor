using OngekiFumenEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 轨道延伸段（Next/End）找不到对应的起点（Start）记录：
    /// 游戏侧仍会把该 recID 注册成一条残缺轨道（recID=-1、laneTagTbl 为空），引用它的 tap/hold
    /// 取 laneTagTbl.laneType 时空引用；出现两条以上残缺轨道则字典重复键直接抛异常。
    /// 编辑器加载时会丢弃这条延伸段，所以缺陷只能由解析层记录（见 fumen.ParseIssues）。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class OrphanLaneRecordCheckRule : IFumenCheckRule
    {
        private const string RuleName = "OrphanLaneRecord";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var issue in fumen.ParseIssues)
            {
                if (issue.Kind != FumenParseIssueKind.LaneStartNotFound)
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.OrphanLaneRecord.Format(issue.Tag, issue.Detail ?? string.Empty),
                    LocationDescription = issue.Line,
                };
            }
        }
    }
}
