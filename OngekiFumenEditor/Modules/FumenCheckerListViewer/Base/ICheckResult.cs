using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
    public interface ICheckResult
    {
        string RuleName { get; }
        RuleSeverity Severity { get; }
        string LocationDescription { get; }

        string Description { get; }

        INavigateBehavior NavigateBehavior { get; }
    }
}
