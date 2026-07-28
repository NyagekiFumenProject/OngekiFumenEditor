using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	internal class MissingRefObjectCheckRule : IFumenCheckRule
	{
		const string RuleName = "MissingRefObject";

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			foreach (var dockableObj in fumen.Holds.AsEnumerable<ILaneDockable>().Concat(fumen.Taps).Where(x => x.ReferenceLaneStart is null))
			{
				yield return new CommonCheckResult()
				{
					Description = Lang.MissingRefObject.Format(dockableObj.GetType().Name),
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
					Description = Lang.MissingRefObject2.Format(dockableObj.GetType().Name, dockableObj.ReferenceLaneStrId),
					LocationDescription = dockableObj.ToString(),
					NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
					RuleName = RuleName,
					Severity = RuleSeverity.Error
				};
			}
		}
	}
}



