using OngekiFumenEditor.Base;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Base
{
    public interface IFumenCheckRule
    {
        IEnumerable<ICheckResult> CheckRule(OngekiFumen fumen, IFumenCheckContext fumenHostEditor);
    }
}
