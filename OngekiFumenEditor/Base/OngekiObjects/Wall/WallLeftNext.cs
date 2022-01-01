using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallLeftNext : WallNext
    {
        public override string IDShortName => "WLN";
        public override Type ModelViewType => typeof(WallNextViewModel<WallLeftNext>);
    }
}
