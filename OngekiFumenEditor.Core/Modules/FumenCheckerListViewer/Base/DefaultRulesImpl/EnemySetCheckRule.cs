using OngekiFumenEditor.Base;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    [Export(typeof(IFumenCheckRule))]
    internal class EnemySetCheckRule : IFumenCheckRule
    {
        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            if (!fumen.EnemySets.Any(x => x.TagTblValue == WaveChangeConst.Boss))
            {
                yield return new CommonCheckResult
                {
                    Description = FumenCheckMessages.Get(FumenCheckMessageKey.MissingBossEnemySet),
                    LocationDescription = string.Empty,
                    RuleName = "MissingBossEnemySet",
                    Severity = RuleSeverity.Suggest
                };
            }
        }
    }
}
