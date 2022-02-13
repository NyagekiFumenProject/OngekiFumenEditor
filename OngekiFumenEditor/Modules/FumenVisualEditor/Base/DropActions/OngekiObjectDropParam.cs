using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions
{
    public class OngekiObjectDropParam : EditorAddObjectDropAction
    {
        private readonly Func<DisplayObjectViewModelBase> lazyLoadFunc;

        public OngekiObjectDropParam(Func<DisplayObjectViewModelBase> lazyLoadFunc)
        {
            this.lazyLoadFunc = lazyLoadFunc;
        }

        protected override DisplayObjectViewModelBase GetDisplayObject()
        {
            return lazyLoadFunc();
        }
    }
}
