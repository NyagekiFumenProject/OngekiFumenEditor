using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Base.OngekiObjects.Beam
{
    public class BeamEnd : BeamChildBase
    {
        public override Type ModelViewType => typeof(BeamEndViewModel);

        public override string IDShortName => "BME";
    }
}
