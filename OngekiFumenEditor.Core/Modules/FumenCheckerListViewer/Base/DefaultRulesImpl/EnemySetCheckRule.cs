using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Properties;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Core.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
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
                    Description = Resources.MissingBossEnemySet,
                    LocationDescription = string.Empty,
                    RuleName = "MissingBossEnemySet",
                    Severity = RuleSeverity.Suggest
                };
            }
        }
    }
}
