using OngekiFumenEditor.Base;
using OngekiFumenEditor.Core.Properties;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.OgkrImpl
{
    [Export(typeof(IFumenCheckRule))]
    [Export(typeof(IOngekiFumenCheckRule))]
    internal class HeaderConstCheckRule : IOngekiFumenCheckRule
    {
        private sealed class NavigateMetaInfoViewBehavior : INavigateBehavior
        {
            public void Navigate(IFumenCheckContext editor) => editor?.ShowFumenMetaInfo();
        }

        public IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostViewModel)
        {
            const string ruleName = "[Ongeki] HeaderConstMismatch";
            var locationDescription = Resources.HeaderConstMismatch;

            if (fumen.MetaInfo.XRESOLUTION != XGrid.DEFAULT_RES_X)
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.HeaderConstMismatch2.Format(fumen.MetaInfo.XRESOLUTION, XGrid.DEFAULT_RES_X),
                    LocationDescription = locationDescription,
                    NavigateBehavior = new NavigateMetaInfoViewBehavior(),
                    RuleName = ruleName,
                };
            }

            if (fumen.MetaInfo.TRESOLUTION != TGrid.DEFAULT_RES_T)
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.HeaderConstMismatch3.Format(fumen.MetaInfo.TRESOLUTION, TGrid.DEFAULT_RES_T),
                    LocationDescription = locationDescription,
                    NavigateBehavior = new NavigateMetaInfoViewBehavior(),
                    RuleName = ruleName,
                };
            }

            if (string.IsNullOrWhiteSpace(fumen.MetaInfo.Creator))
            {
                yield return new CommonCheckResult
                {
                    Severity = RuleSeverity.Error,
                    Description = Resources.HeaderConstMismatch4,
                    LocationDescription = locationDescription,
                    NavigateBehavior = new NavigateMetaInfoViewBehavior(),
                    RuleName = ruleName,
                };
            }
        }
    }
}
