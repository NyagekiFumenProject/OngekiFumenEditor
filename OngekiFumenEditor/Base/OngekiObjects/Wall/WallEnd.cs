using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallEnd : WallChildBase
    {
        public override Type ModelViewType => typeof(WallEndViewModel);

        public override string IDShortName => "WLE";
    }
}
