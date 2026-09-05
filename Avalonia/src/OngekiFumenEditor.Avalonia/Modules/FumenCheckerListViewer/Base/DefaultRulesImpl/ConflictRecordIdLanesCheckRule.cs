using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;

[RegisterSingleton]
internal sealed class ConflictRecordIdLanesCheckRule : IFumenCheckRule
{
    public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
    {
        const string ruleName = "ConflictRecordIdLanes";

        foreach (var conflictGroup in fumen.Lanes
                     .GroupBy(lane => lane.RecordId)
                     .Where(group => group.Skip(1).Any()))
        {
            var firstLane = conflictGroup.First();
            yield return new CommonCheckResult
            {
                Severity = RuleSeverity.Error,
                Description = $"Duplicate lane RecordId detected: {conflictGroup.Key}",
                LocationDescription = $"RecordId={conflictGroup.Key} {firstLane.XGrid} {firstLane.TGrid}",
                NavigateBehavior = new NavigateToTGridBehavior(firstLane.TGrid),
                RuleName = ruleName
            };
        }
    }
}
