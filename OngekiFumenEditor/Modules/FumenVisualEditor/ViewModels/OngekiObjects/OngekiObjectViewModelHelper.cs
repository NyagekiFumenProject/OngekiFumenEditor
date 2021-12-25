using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects
{
    public static class OngekiObjectViewModelHelper
    {
        public static DisplayObjectViewModelBase CreateViewModel(IDisplayableObject ongekiObject)
        {
            return Activator.CreateInstance(ongekiObject.ModelViewType) as DisplayObjectViewModelBase;
        }
    }
}
