using OngekiFumenEditor.Core.Base;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Modules.FumenCheckerListViewer.Base
{
    public interface IFumenCheckRule
    {
        IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostEditor);
    }
}
