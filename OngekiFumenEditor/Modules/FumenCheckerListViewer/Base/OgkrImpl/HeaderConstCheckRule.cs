using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr.Rules;
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
			const string LocationDescription = $"谱面Header信息";

			if (fumen.MetaInfo.XRESOLUTION != XGrid.DEFAULT_RES_X)
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = $"谱面XRESOLUTION({fumen.MetaInfo.XRESOLUTION})参数和XGrid.ResX({XGrid.DEFAULT_RES_X})不匹配",
					LocationDescription = LocationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}

			if (fumen.MetaInfo.TRESOLUTION != TGrid.DEFAULT_RES_T)
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = $"TRESOLUTION({fumen.MetaInfo.TRESOLUTION})TGrid.ResT({TGrid.DEFAULT_RES_T})不匹配",
					LocationDescription = LocationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}

			if (string.IsNullOrWhiteSpace(fumen.MetaInfo.Creator))
			{
				yield return new CommonCheckResult()
				{
					Severity = RuleSeverity.Error,
					Description = $"无谱师信息",
					LocationDescription = LocationDescription,
					NavigateBehavior = new NavigateMetaInfoViewBehavior(),
					RuleName = RuleName,
				};
			}
		}
	}
}

