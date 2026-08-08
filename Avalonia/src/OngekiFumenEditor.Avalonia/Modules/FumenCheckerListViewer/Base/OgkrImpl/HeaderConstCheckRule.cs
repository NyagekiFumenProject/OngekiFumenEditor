using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base.OgkrImpl
{

	[RegisterSingleton]
	internal class HeaderConstCheckRule : IOngekiFumenCheckRule
	{
		private class NavigateMetaInfoViewBehavior : INavigateBehavior
		{
			public void Navigate(FumenVisualEditorViewModel editor)
			{
				// Pending actual tool wiring when FumenMetaInfoBrowser integration is finalized.
			}
		}

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			const string RuleName = "[Ongeki] HeaderConstMismatch";
			var locationDescription = Lang.HeaderConstMismatch;

			if (fumen.MetaInfo.XRESOLUTION != XGrid.DEFAULT_RES_X)
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = Lang.HeaderConstMismatch2.Format(fumen.MetaInfo.XRESOLUTION, XGrid.DEFAULT_RES_X),
					LocationDescription = locationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}

			if (fumen.MetaInfo.TRESOLUTION != TGrid.DEFAULT_RES_T)
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = Lang.HeaderConstMismatch3.Format(fumen.MetaInfo.TRESOLUTION, TGrid.DEFAULT_RES_T),
					LocationDescription = locationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}

			if (string.IsNullOrWhiteSpace(fumen.MetaInfo.Creator))
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = Lang.HeaderConstMismatch4,
					LocationDescription = locationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}
		}
	}
}




