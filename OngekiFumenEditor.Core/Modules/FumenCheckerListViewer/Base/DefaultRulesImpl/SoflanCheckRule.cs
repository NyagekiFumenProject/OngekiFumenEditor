using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class SoflanCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            foreach (KeyValuePair<int, SoflanList> pair in fumen.SoflansMap)
            {
                var r = pair.Value.CalculateSpeed(fumen.BpmList, TGrid.MaxValue);
                var lastTGrid = pair.Value.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList).LastOrDefault().TGrid;

                if (r != 1)
                {
                    yield return new CommonCheckResult
                    {
                        Description = FumenCheckMessages.Get(FumenCheckMessageKey.CheckRuleSoflanProblem, r),
                        LocationDescription = $"SoflanGroup: {pair.Key}, TGrid: {lastTGrid}",
                        NavigateBehavior = new NavigateToTGridBehavior(lastTGrid),
                        RuleName = "Soflan",
                        Severity = RuleSeverity.Problem
                    };
                }
            }
        }
    }
}
