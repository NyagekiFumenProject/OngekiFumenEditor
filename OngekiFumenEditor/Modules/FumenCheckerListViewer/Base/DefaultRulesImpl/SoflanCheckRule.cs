using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	internal class SoflanCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			var r = fumen.Soflans.CalculateSpeed(fumen.BpmList, TGrid.MaxValue);
			var lastTGrid = fumen.Soflans.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList).LastOrDefault().TGrid;

			if (r != 1)
			{
				yield return new CommonCheckResult()
				{
					Description = Resources.CheckRuleSoflanProblem.Format(r),
					LocationDescription = lastTGrid?.ToString(),
					NavigateBehavior = new NavigateToTGridBehavior(lastTGrid),
					RuleName = "Soflan",
					Severity = RuleSeverity.Problem
				};
			}
		}
	}
}
