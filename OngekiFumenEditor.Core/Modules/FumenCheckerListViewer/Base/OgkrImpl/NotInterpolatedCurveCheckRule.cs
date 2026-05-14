using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.OgkrImpl
{
    [Export(typeof(IFumenCheckRule))]
    [Export(typeof(IOngekiFumenCheckRule))]
    internal class NotInterpolatedCurveCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "[Ongeki] NotInterpolatedCurve";

            foreach (var obj in fumen.GetAllDisplayableObjects().OfType<ConnectableChildObjectBase>().Where(x => x.IsCurvePath))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Problem,
                    Description = Resources.NotInterpolatedCurve,
                    LocationDescription = $"{obj.XGrid} {obj.TGrid}",
                    NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
                    RuleName = ruleName,
                };
            }
        }
    }
}
