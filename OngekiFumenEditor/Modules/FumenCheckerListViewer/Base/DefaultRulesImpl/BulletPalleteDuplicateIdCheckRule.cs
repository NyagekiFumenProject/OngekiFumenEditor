using OngekiFumenEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 弹种（BPL）ID 重复：游戏侧 BulletPalleteList 以 strID 为字典键，重复 Add 抛异常（读谱崩溃）。
    /// 编辑器加载时会静默用新模板替换旧模板，所以缺陷只能由解析层记录（见 fumen.ParseIssues）。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class BulletPalleteDuplicateIdCheckRule : IFumenCheckRule
    {
        private const string RuleName = "BulletPalleteDuplicateId";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (var issue in fumen.ParseIssues)
            {
                if (issue.Kind != FumenParseIssueKind.BulletPalleteIdDuplicate)
                    continue;

                yield return new CommonCheckResult
                {
                    RuleName = RuleName,
                    Severity = RuleSeverity.Error,
                    Description = Resources.BulletPalleteDuplicateId.Format(issue.Detail ?? string.Empty),
                    LocationDescription = issue.Line,
                };
            }
        }
    }
}
