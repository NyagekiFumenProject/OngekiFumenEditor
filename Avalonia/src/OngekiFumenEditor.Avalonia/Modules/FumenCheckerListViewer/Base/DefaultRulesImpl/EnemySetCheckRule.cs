using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Collections.Generic;
using System.Linq;
using static OngekiFumenEditor.Avalonia.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	internal class EnemySetCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			if (!fumen.EnemySets.Any(x => x.TagTblValue == WaveChangeConst.Boss))
			{
				yield return new CommonCheckResult()
				{
					Description = Lang.MissingBossEnemySet,
					LocationDescription = string.Empty,
					RuleName = "MissingBossEnemySet",
					Severity = RuleSeverity.Suggest
				};
			}
		}
	}
}




