using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.OgkrImpl
{

	internal class NotInterpolatedCurveCheckRule : IOngekiFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			IEnumerable<ICheckResult> CheckList(IEnumerable<ConnectableChildObjectBase> objs)
			{
				const string RuleName = "[Ongeki] NotInterpolatedCurve";

				foreach (var obj in objs.Where(x => x.IsCurvePath))
				{
					yield return new CommonCheckResult()
					{
						Severity = RuleSeverity.Problem,
						Description = Lang.NotInterpolatedCurve,
						LocationDescription = $"{obj.XGrid} {obj.TGrid}",
						NavigateBehavior = new NavigateToTGridBehavior(obj.TGrid),
						RuleName = RuleName,
					};
				}
			}

			foreach (var result in CheckList(fumen.GetAllDisplayableObjects().OfType<ConnectableChildObjectBase>()))
				yield return result;
		}
	}
}




