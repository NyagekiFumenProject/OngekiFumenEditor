using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class InvalidConnectablePathCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "InvalidConnectablePath";

            foreach (var obj in fumen.GetAllDisplayableObjects().OfType<ConnectableChildObjectBase>().Where(x => !x.IsVaildPath))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Problem,
                    Description = Resources.InvalidConnectablePath,
                    LocationDescription = $"{obj.XGrid} {obj.TGrid}",
                    NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
                    RuleName = ruleName,
                };
            }
        }
    }
}
