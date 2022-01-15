using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    [Flags]
    public enum RedrawTarget
    {
        OngekiObjects = 1,
        TGridUnitLines = 2,
        XGridUnitLines = 4,
        ScrollBar = 8,

        All = OngekiObjects | TGridUnitLines | XGridUnitLines,
        UnitLines = TGridUnitLines | XGridUnitLines,
    }
}
