using System.Collections.Generic;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
[RegisterSingleton]
public class LaneBlockOnMultipleWallsCheckRule : IFumenCheckRule
{
    private const string RuleName = "LaneBlockAcrossWalls";

    public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostEditor)
    {
        foreach (var laneBlock in fumen.LaneBlocks) {
            var (refLaneStart, refLaneEnd) = laneBlock.CalculateReferenceWallLanes(fumen);
            if (refLaneStart != refLaneEnd) {
                yield return new CommonCheckResult()
                {
                    Severity = RuleSeverity.Problem,
                    Description = Lang.LaneBlockOnMultipleWalls.Format(refLaneStart?.RecordId, refLaneEnd?.RecordId),
                    LocationDescription = laneBlock.TGrid.ToString(),
                    NavigateBehavior = new NavigateToTGridBehavior(refLaneEnd?.ReferenceStartObject.TGrid),
                    RuleName = RuleName
                };
            }
        }
    }
}


