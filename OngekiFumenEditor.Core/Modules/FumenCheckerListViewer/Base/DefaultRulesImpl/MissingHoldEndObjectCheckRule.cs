using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class MissingHoldEndObjectCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "MissingHoldEndObject";

            foreach (var missingObject in fumen.Holds.Where(x => x.HoldEnd is null))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = FumenCheckMessages.Get(FumenCheckMessageKey.MissingEndObject, missingObject.Id),
                    LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
                    NavigateBehavior = new NavigateToObjectBehavior(missingObject),
                    RuleName = ruleName,
                };
            }
        }
    }
}
