using OngekiFumenEditor.Base;
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
        private readonly Func<OngekiObjectBase> lazyLoadFunc;

        public OngekiObjectDropParam(Func<OngekiObjectBase> lazyLoadFunc)
        {
            this.lazyLoadFunc = lazyLoadFunc;
        }

        protected override OngekiObjectBase GetDisplayObject()
        {
            return lazyLoadFunc();
        }
    }
}
