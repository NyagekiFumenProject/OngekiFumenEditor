using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects.Projectiles;
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
    internal class BulletNullPalleteCheckRule : IOngekiFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "[Ongeki] BulletNullPallete";

            foreach (var bullet in fumen.GetAllDisplayableObjects().OfType<Bullet>().Where(x => x.ReferenceBulletPallete is null))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.BulletNullPalleteDescription.Format(bullet.Id),
                    LocationDescription = $"{bullet.XGrid} {bullet.TGrid}",
                    NavigateBehavior = new NavigateToObjectBehavior(bullet),
                    RuleName = ruleName,
                };
            }
        }
    }
}

