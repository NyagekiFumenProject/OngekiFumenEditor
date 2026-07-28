using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    internal class SoflanCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            foreach (KeyValuePair<int, SoflanList> pair in fumen.SoflansMap)
            {
                var r = pair.Value.CalculateSpeed(fumen.BpmList, TGrid.MaxValue);
                var lastTGrid = pair.Value.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList).LastOrDefault().TGrid;

                if (r != 1)
                {
                    yield return new CommonCheckResult()
                    {
                        Description = Lang.CheckRuleSoflanProblem.Format(r),
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



