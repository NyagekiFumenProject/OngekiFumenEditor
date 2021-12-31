using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallNext : WallChildBase
    {
        public override Type ModelViewType => typeof(WallNextViewModel);

        public override string IDShortName => "WLN";
    }
}
