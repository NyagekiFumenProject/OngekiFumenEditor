using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public class OngekiObjectDropParam
    {
        public OngekiObjectDropParam(Func<DisplayObjectViewModelBase> lazyLoadFunc)
        {
            OngekiObjectViewModel = new Lazy<DisplayObjectViewModelBase>(lazyLoadFunc);
        }

        public Lazy<DisplayObjectViewModelBase> OngekiObjectViewModel { get; }
    }
}
