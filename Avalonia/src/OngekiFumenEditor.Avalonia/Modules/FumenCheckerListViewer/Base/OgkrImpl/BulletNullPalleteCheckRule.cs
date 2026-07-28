using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
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

    internal class BulletNullPalleteCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
        {
            IEnumerable<ICheckResult> CheckList(IEnumerable<Bullet> objs)
            {
                const string RuleName = "[Ongeki] BulletNullPallete";

                foreach (var bullet in objs.Where(x => x.ReferenceBulletPallete is null))
                {
                    yield return new CommonCheckResult()
                    {
                        Severity = RuleSeverity.Error,
                        Description = Lang.BulletNullPalleteDescription.Format(bullet.Id),
                        LocationDescription = $"{bullet.XGrid} {bullet.TGrid}",
                        NavigateBehavior = new NavigateToObjectBehavior(bullet),
                        RuleName = RuleName,
                    };
                }
            }

            foreach (var result in CheckList(fumen.GetAllDisplayableObjects().OfType<Bullet>()))
                yield return result;
        }
    }
}




