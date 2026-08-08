using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using System.Collections.Generic;
using System.Linq;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[RegisterSingleton]
	internal class InvalidConnectablePathCheckRule : IFumenCheckRule
	{
		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			IEnumerable<ICheckResult> CheckList(IEnumerable<ConnectableChildObjectBase> objs)
			{
				const string RuleName = "InvalidConnectablePath";

				foreach (var obj in objs.Where(x => !x.IsVaildPath))
				{
					yield return new CommonCheckResult()
					{
						Severity = RuleSeverity.Problem,
						Description = Lang.InvalidConnectablePath,
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




