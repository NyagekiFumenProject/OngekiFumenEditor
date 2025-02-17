using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr.Rules;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    [Export(typeof(IOngekiFumenCheckRule))]
    internal class ColorIdCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList(IEnumerable<ColorfulLaneStart> objs)
            {
                const string RuleName = "[Ongeki] ColorIdInvaild";

                foreach (var obj in objs.Where(x => !ColorIdConst.AllColors.Any(t => t.Id == x.ColorId.Id)))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = Resources.InvalidColorId.Format(obj.ColorId.ToString()),
                        LocationDescription = $"{obj.XGrid} {obj.TGrid}",
                        NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
                        RuleName = RuleName,
                    };
                }
            }

            foreach (var result in CheckList(fumen.GetAllDisplayableObjects().OfType<ColorfulLaneStart>()))
                yield return result;
        }
    }
}

