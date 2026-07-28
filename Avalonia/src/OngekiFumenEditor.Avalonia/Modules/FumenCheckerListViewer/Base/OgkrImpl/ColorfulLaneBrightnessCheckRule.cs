using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.OgkrImpl
{

    internal class ColorfulLaneBrightnessCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList(IEnumerable<ColorfulLaneStart> objs)
            {
                const string RuleName = "[Ongeki] ColorfulLaneBrightnessInvaild";

                foreach (var obj in objs.Where(x => x.Brightness > 3 || x.Brightness < -3))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = Lang.InvalidBrightness.Format(obj.Brightness),
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




