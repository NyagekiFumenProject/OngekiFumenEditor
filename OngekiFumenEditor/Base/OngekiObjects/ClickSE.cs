using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class ClickSE : OngekiTimelineObjectBase
    {
        public static string CommandName => "CLK";
        public override string IDShortName => CommandName;
    }
}
