using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Core.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class ConflictRecordIdLanesCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "ConflictRecordIdLanes";

            foreach (var conflictGroup in fumen.Lanes.GroupBy(x => x.RecordId).Where(x => x.AtLeastCount(2)))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = $"Duplicate lane RecordId detected: {conflictGroup.Key}",
                    LocationDescription = $"RecordId={conflictGroup.Key} {conflictGroup.First().XGrid} {conflictGroup.First().TGrid}",
                    NavigateBehavior = new NavigateToTGridBehavior(conflictGroup.First().TGrid),
                    RuleName = ruleName,
                };
            }
        }
    }
}

