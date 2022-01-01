using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallRightEnd : WallEnd
    {
        public override string IDShortName => "WRE";
        public override Type ModelViewType => typeof(WallEndViewModel<WallRightEnd>);
    }
}
