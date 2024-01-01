using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
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
	internal class MissingEndObjectCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			const string RuleName = "MissingEndObject";

			//IEnumerable<ICheckResult> CheckBeamList(IEnumerable<BeamStart> objs)
			//{
			//	foreach (var missingObject in objs.Where(x => !x.Children.OfType<ConnectableEndObject>().Any()))
			//	{
			//		yield return new CommonCheckResult()
			//		{
			//			Severity = RuleSeverity.Problem,
			//			Description = $"物件{missingObject.IDShortName}(id:{missingObject.RecordId})缺少中止物件",
			//			LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
			//			NavigateBehavior = new NavigateToObjectBehavior(missingObject),
			//			RuleName = RuleName,
			//		};
			//	}
			//}

			//IEnumerable<ICheckResult> CheckList(IEnumerable<ConnectableStartObject> objs)
			//{
			//	foreach (var missingObject in objs.Where(x => !x.Children.OfType<ConnectableEndObject>().Any()))
			//	{
			//		yield return new CommonCheckResult()
			//		{
			//			Severity = RuleSeverity.Problem,
			//			Description = $"物件{missingObject.IDShortName}(id:{missingObject.RecordId})缺少中止物件",
			//			LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
			//			NavigateBehavior = new NavigateToObjectBehavior(missingObject),
			//			RuleName = RuleName,
			//		};
			//	}
			//}

			IEnumerable<ICheckResult> CheckHoldList(IEnumerable<Hold> objs)
			{
				foreach (var missingObject in objs.Where(x => x.HoldEnd is null))
				{
					yield return new CommonCheckResult()
					{
						Severity = RuleSeverity.Error,
						Description = Resources.MissingEndObject.Format(missingObject.Id),
						LocationDescription = $"{missingObject.XGrid} {missingObject.TGrid}",
						NavigateBehavior = new NavigateToObjectBehavior(missingObject),
						RuleName = RuleName,
					};
				}
			}

			var starts = Enumerable.Empty<ConnectableStartObject>()
				.Concat(fumen.Lanes);

			//foreach (var start in CheckList(starts))
			//{
			//	yield return start;
			//}

			//foreach (var start in CheckBeamList(fumen.Beams))
			//{
			//	yield return start;
			//}

			foreach (var start in CheckHoldList(fumen.Holds))
			{
				yield return start;
			}
		}
	}
}

