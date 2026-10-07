using OngekiFumenEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 色带轨道记录（CLS/CLN/CLE）列数不足：color/brightness 固定在第 5/6 列，
    /// 游戏侧按列索引直接读取，缺列越界崩溃；编辑器侧会把缺读到的字段静默写成默认值（回写丢列）。
    /// 缺陷由解析层记录（见 fumen.ParseIssues）。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class ColorfulLaneRecordColumnsCheckRule : IFumenCheckRule
    {
        private const string RuleName = "ColorfulLaneRecordColumns";

        private static readonly string[] ColorfulLaneCommands = { "CLS", "CLN", "CLE" };

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var issue in fumen.ParseIssues)
            {
                if (issue.Kind != FumenParseIssueKind.RecordColumnTooFew)
                    continue;
                if (!ColorfulLaneCommands.Contains(issue.Tag))
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.RecordColumnTooFew.Format(issue.Tag, issue.RequiredColumns, issue.ActualColumns),
                    LocationDescription = issue.Line,
                };
            }
        }
    }
}
