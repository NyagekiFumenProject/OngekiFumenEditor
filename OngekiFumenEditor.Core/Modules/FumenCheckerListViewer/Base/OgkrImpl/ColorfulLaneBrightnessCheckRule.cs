using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using OngekiFumenEditor.Core.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.OgkrImpl
{
    [Export(typeof(IFumenCheckRule))]
    [Export(typeof(IOngekiFumenCheckRule))]
    internal class ColorfulLaneBrightnessCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "[Ongeki] ColorfulLaneBrightnessInvaild";

            foreach (var obj in fumen.GetAllDisplayableObjects().OfType<ColorfulLaneStart>().Where(x => x.Brightness > 3 || x.Brightness < -3))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.InvalidBrightness.Format(obj.Brightness),
                    LocationDescription = $"{obj.XGrid} {obj.TGrid}",
                    NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
                    RuleName = ruleName,
                };
            }
        }
    }
}

