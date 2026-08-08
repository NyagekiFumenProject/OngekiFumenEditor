using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[RegisterSingleton]
	internal class MissingHoldEndObjectCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			const string RuleName = "MissingHoldEndObject";

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
						Description = Lang.MissingEndObject.Format(missingObject.Id),
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




