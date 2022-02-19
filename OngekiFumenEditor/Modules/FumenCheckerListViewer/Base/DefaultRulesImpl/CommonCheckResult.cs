using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultRulesImpl
{
    public struct CommonCheckResult : ICheckResult
    {
        public string RuleName { get; set; }

        public RuleSeverity Severity { get; set; }

        public string LocationDescription { get; set; }
        public string Description { get; set; }

        public TGrid NavigateTGridLocation { get; set; }

        public void Navigate(object fumenHostedObj)
        {
            if (fumenHostedObj is FumenVisualEditorViewModel editor)
            {
                editor.ScrollTo(NavigateTGridLocation);
            }
        }
    }
}
