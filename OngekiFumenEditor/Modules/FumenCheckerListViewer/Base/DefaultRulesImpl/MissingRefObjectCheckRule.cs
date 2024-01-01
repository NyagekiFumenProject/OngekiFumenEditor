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
	internal class MissingRefObjectCheckRule : IFumenCheckRule
	{
		const string RuleName = "MissingRefObject";

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			foreach (var dockableObj in fumen.Holds.AsEnumerable<ILaneDockable>().Concat(fumen.Taps).Where(x => x.ReferenceLaneStart is null))
			{
				yield return new CommonCheckResult()
				{
					Description = Resources.MissingRefObject.Format(dockableObj.GetType().Name),
					LocationDescription = dockableObj.ToString(),
					NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
					RuleName = RuleName,
					Severity = RuleSeverity.Error
				};
			}

			foreach (var dockableObj in fumen.Holds.AsEnumerable<ILaneDockable>().Concat(fumen.Taps).Where(x => !fumen.Lanes.Contains(x.ReferenceLaneStart)))
			{
				yield return new CommonCheckResult()
				{
					Description = Resources.MissingRefObject2.Format(dockableObj.GetType().Name, dockableObj.ReferenceLaneStrId),
					LocationDescription = dockableObj.ToString(),
					NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
					RuleName = RuleName,
					Severity = RuleSeverity.Error
				};
			}
		}
	}
}
