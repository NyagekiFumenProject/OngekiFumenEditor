using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[RegisterSingleton]
	internal class DockableObjectWrongLocationCheckRule : IFumenCheckRule
	{
		const string RuleName = "WrongLocation";

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			foreach (var dockableObj in fumen.Holds
				.AsEnumerable<ILaneDockable>()
				.Concat(fumen.Taps)
				.Where(x => x.ReferenceLaneStart is not null)
				.Where(x => CheckIsWrongLocation(x, x.ReferenceLaneStart))
				)
			{
				yield return new CommonCheckResult()
				{
					Description = Lang.WrongLocation.Format(dockableObj.GetType().Name, dockableObj.ReferenceLaneStrId),
					LocationDescription = dockableObj.ToString(),
					NavigateBehavior = new NavigateToObjectBehavior(dockableObj as OngekiTimelineObjectBase),
					RuleName = RuleName,
					Severity = RuleSeverity.Problem
				};
			}
		}

		private bool CheckIsWrongLocation(ILaneDockable x, LaneStartBase referenceLaneStart)
		{
			if (x.TGrid > referenceLaneStart.MaxTGrid || x.TGrid < referenceLaneStart.MinTGrid)
				return true;

			var calXGrid = referenceLaneStart.CalulateXGrid(x.TGrid);
			if (calXGrid is null)
				return false;

			return Math.Abs((calXGrid - x.XGrid).TotalGrid(calXGrid.ResX)) > calXGrid.ResX * 1;
		}
	}
}



