using OngekiFumenEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 光束记录（BMS/BMN/BME、OBS/OBN/OBE）列数不足：widthId 固定在第 5 列、斜向光束的 shootPosDiff
    /// 固定在第 6 列，游戏侧按列索引直接读取，缺列越界崩溃。缺陷由解析层记录（见 fumen.ParseIssues）。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class BeamRecordColumnsCheckRule : IFumenCheckRule
    {
        private const string RuleName = "BeamRecordColumns";

        private static readonly string[] BeamCommands = { "BMS", "BMN", "BME", "OBS", "OBN", "OBE" };

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var issue in fumen.ParseIssues)
            {
                if (issue.Kind != FumenParseIssueKind.RecordColumnTooFew)
                    continue;
                if (!BeamCommands.Contains(issue.Tag))
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
