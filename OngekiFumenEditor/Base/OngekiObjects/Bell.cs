using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class Bell : OngekiMovableObjectBase
    {
        public static string CommandName => "BEL";
        public override string IDShortName => CommandName;

        public override Type ModelViewType => typeof(BellViewModel);
    }
}
