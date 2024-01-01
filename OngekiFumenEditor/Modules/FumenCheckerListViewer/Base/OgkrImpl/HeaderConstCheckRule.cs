using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr.Rules;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
	[Export(typeof(IFumenCheckRule))]
	[Export(typeof(IOngekiFumenCheckRule))]
	internal class HeaderConstCheckRule : IOngekiFumenCheckRule
	{
		private class NavigateMetaInfoViewBehavior : INavigateBehavior
		{
			public void Navigate(FumenVisualEditorViewModel editor)
			{
				IoC.Get<IShell>().ShowTool<IFumenMetaInfoBrowser>();
			}
		}

		public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, FumenVisualEditorViewModel fumenHostViewModel)
		{
			const string RuleName = "[Ongeki] HeaderConstMismatch";
			var locationDescription = Resources.HeaderConstMismatch;

			if (fumen.MetaInfo.XRESOLUTION != XGrid.DEFAULT_RES_X)
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = Resources.HeaderConstMismatch2.Format(fumen.MetaInfo.XRESOLUTION, XGrid.DEFAULT_RES_X),
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
					Description = Resources.HeaderConstMismatch3.Format(fumen.MetaInfo.TRESOLUTION, TGrid.DEFAULT_RES_T),
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
					Description = Resources.HeaderConstMismatch4,
					LocationDescription = locationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}
		}
	}
}

