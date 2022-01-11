using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class WallHoldEnd : HoldEnd
    {
        public override Type ModelViewType => typeof(WallHoldEndViewModel);
    }
}
