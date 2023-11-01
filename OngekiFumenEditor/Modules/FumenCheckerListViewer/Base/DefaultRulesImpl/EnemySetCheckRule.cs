using AngleSharp.Css;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	internal class EnemySetCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			if (!fumen.EnemySets.Any(x => x.TagTblValue == WaveChangeConst.Boss))
			{
				yield return new CommonCheckResult()
				{
					Description = $"缺少Boss的EnemySet物件,建议放置一个来钦定boss出现的时机",
					LocationDescription = string.Empty,
					RuleName = "MissingBossEnemySet",
					Severity = RuleSeverity.Suggest
				};
			}
		}
	}
}
