using AngleSharp.Css;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

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
					Description = $"最后一个Soflan变速后应该变回正常的1速,但现在是{r}速",
					LocationDescription = lastTGrid?.ToString(),
					NavigateBehavior = new NavigateToTGridBehavior(lastTGrid),
					RuleName = "Soflan",
					Severity = RuleSeverity.Problem
				};
			}
		}
	}
}
