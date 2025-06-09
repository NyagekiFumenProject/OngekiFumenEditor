using System.Collections.Generic;
using System.ComponentModel.Composition;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;

[Export(typeof(IFumenCheckRule))]
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
                    Description = Resources.LaneBlockOnMultipleWalls.Format(refLaneStart?.RecordId, refLaneEnd?.RecordId),
                    LocationDescription = laneBlock.TGrid.ToString(),
                    NavigateBehavior = new NavigateToTGridBehavior(refLaneEnd?.ReferenceStartObject.TGrid),
                    RuleName = RuleName
                };
            }
        }
    }
}