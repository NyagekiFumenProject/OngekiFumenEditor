using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class ConflictRecordIdLanesCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList(IEnumerable<IGrouping<int, OngekiFumenEditor.Base.OngekiObjects.Lane.Base.LaneStartBase>> conflictLaneGroups)
            {
                const string RuleName = "ConflictRecordIdLanes";

                foreach (var conflictGroup in conflictLaneGroups)
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = "存在多条相同RecordId的轨道",
                        LocationDescription = $"RecordId={conflictGroup.Key} {conflictGroup.First().XGrid} {conflictGroup.First().TGrid}",
                        NavigateBehavior = new NavigateToTGridBehavior(conflictGroup.First().TGrid),
                        RuleName = RuleName,
                    };
                }
            }

            foreach (var result in CheckList(fumen.Lanes.GroupBy(x => x.RecordId).Where(x => x.AtLeastCount(2))))
                yield return result;
        }
    }
}

