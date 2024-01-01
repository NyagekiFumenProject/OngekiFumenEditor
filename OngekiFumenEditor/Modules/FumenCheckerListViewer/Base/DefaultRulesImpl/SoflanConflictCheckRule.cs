using AngleSharp.Css;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	internal class SoflanConflictCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			bool IsConflict(double start1, double end1, double start2, double end2) => (start1 < end2 && start2 < end1) || (start2 < end1 && start1 < end2);
			var conflictRecMap = new HashSet<OngekiTimelineObjectBase>();

			foreach (var cur in fumen.Soflans)
			{
				var affSoflans = fumen.Soflans.GetVisibleStartObjects(cur.TGrid, cur.EndTGrid).Except(new[] { cur });

				foreach (var aff in affSoflans)
				{
					if (IsConflict(aff.TGrid.TotalUnit, aff.EndTGrid.TotalUnit, cur.TGrid.TotalUnit, cur.EndTGrid.TotalUnit))
					{
						var r = new[] { aff, cur }.OfType<OngekiTimelineObjectBase>().MaxBy(x => x.Id);
						if (conflictRecMap.Contains(r))
							continue;
						conflictRecMap.Add(r);

						yield return new CommonCheckResult()
						{
							Description = Resources.SoflanConflict.Format(cur, aff),
							LocationDescription = $"({cur.TGrid},{cur.EndTGrid}) - ({aff.TGrid},{aff.EndTGrid})",
							NavigateBehavior = new NavigateToObjectBehavior(r),
							RuleName = "SoflanConflict",
							Severity = RuleSeverity.Error
						};
					}
				}
			}
		}
	}
}
