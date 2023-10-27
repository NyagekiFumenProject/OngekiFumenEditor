using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
					Description = $"{dockableObj.GetType().Name}物件缺少引用Lane物件",
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
					Description = $"{dockableObj.GetType().Name}物件引用的Lane物件(laneId:{dockableObj.ReferenceLaneStrId}),不存在于谱面文件内",
					LocationDescription = dockableObj.ToString(),
					NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
					RuleName = RuleName,
					Severity = RuleSeverity.Error
				};
			}
		}
	}
}
