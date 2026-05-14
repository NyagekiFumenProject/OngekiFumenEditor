using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
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
    internal class ColorIdCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "[Ongeki] ColorIdInvaild";

            foreach (var obj in fumen.GetAllDisplayableObjects().OfType<ColorfulLaneStart>().Where(x => !ColorIdConst.AllColors.Any(t => t.Id == x.ColorId.Id)))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.InvalidColorId.Format(obj.ColorId.ToString()),
                    LocationDescription = $"{obj.XGrid} {obj.TGrid}",
                    NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
                    RuleName = ruleName,
                };
            }
        }
    }
}

