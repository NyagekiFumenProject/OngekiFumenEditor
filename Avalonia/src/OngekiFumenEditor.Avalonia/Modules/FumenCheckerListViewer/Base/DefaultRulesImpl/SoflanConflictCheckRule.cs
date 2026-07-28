using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;
using static OngekiFumenEditor.Avalonia.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    internal class SoflanConflictCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            bool IsConflict(double start1, double end1, double start2, double end2) => (start1 < end2 && start2 < end1) || (start2 < end1 && start1 < end2);
            var conflictRecMap = new HashSet<OngekiTimelineObjectBase>();

            foreach (KeyValuePair<int, SoflanList> pair in fumen.SoflansMap)
            {
                foreach (var cur in pair.Value)
                {
                    var affSoflans = pair.Value.GetVisibleStartObjects(cur.TGrid, cur.EndTGrid).Except(new[] { cur });

                    foreach (var aff in affSoflans)
                    {
                        if (IsConflict(aff.TGrid.TotalUnit, aff.EndTGrid.TotalUnit, cur.TGrid.TotalUnit, cur.EndTGrid.TotalUnit))
                        {
                            var r = new[] { aff, cur }.OfType<OngekiTimelineObjectBase>().MaxBy(x => x.Id);
                            if (conflictRecMap.Contains(r))
                                continue;
                            conflictRecMap.Add(r);

                            yield return new CommonCheckResult()
                            {
                                Description = Lang.SoflanConflict.Format(cur, aff),
                                LocationDescription = $"SoflanGroup:{pair.Key} ({cur.TGrid},{cur.EndTGrid}) - ({aff.TGrid},{aff.EndTGrid})",
                                NavigateBehavior = new NavigateToObjectBehavior(r),
                                RuleName = "SoflanConflict",
                                Severity = RuleSeverity.Error
                            };
                        }
                    }
                }
            }
        }
    }
}




