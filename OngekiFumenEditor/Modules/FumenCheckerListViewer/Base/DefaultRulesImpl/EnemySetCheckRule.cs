using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    /// <summary>
    /// 谱面存在 EnemySet（EST）却缺少 WAVE1 或 BOSS：
    /// 游戏侧 NotesManager 要求两者齐备（<c>if (!flag2 || !flag)</c>），任一缺失就清空整组 EST 并改用
    /// 默认波次布局（Zako01@frame0 + Boss@frame3600）——作者编写的波次时机/数量/属性全部失效。
    /// </summary>
    [Export(typeof(IFumenCheckRule))]
    internal class EnemySetCheckRule : IFumenCheckRule
    {
        private const string RuleName = "MissingEnemySetWave";

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            var enemySets = fumen.EnemySets;
            if (enemySets.Count == 0)
                yield break;

            var hasWave1 = enemySets.Any(x => x.TagTblValue == WaveChangeConst.Wave1);
            var hasBoss = enemySets.Any(x => x.TagTblValue == WaveChangeConst.Boss);

            if (hasWave1 && hasBoss)
                yield break;

            var missing = (hasWave1, hasBoss) switch
            {
                (false, false) => "WAVE1 + BOSS",
                (false, true) => "WAVE1",
                _ => "BOSS",
            };

            yield return new CommonCheckResult
            {
                Description = Resources.MissingEnemySetWave.Format(missing),
                LocationDescription = enemySets.First().TGrid.ToString(),
                RuleName = RuleName,
                Severity = RuleSeverity.Error,
                NavigateBehavior = new NavigateToObjectBehavior(enemySets.First()),
            };
        }
    }
}
