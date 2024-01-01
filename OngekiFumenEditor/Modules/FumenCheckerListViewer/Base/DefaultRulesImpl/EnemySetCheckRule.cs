using AngleSharp.Css;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
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
					Description = Resources.MissingBossEnemySet,
					LocationDescription = string.Empty,
					RuleName = "MissingBossEnemySet",
					Severity = RuleSeverity.Suggest
				};
			}
		}
	}
}
