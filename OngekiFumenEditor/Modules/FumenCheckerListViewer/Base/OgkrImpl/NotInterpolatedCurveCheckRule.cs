using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr.Rules;
using OngekiFumenEditor.Properties;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	[Export(typeof(IOngekiFumenCheckRule))]
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
						Description = Resources.NotInterpolatedCurve,
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

