using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class ColorIdEnumTypeUIViewModel : CommonUIViewModelBase
    {
        public IEnumerable<ColorId> EnumValues => ColorIdConst.AllColors;

        public ColorIdEnumTypeUIViewModel(PropertyInfoWrapper wrapper) : base(new PropertyInfoWrapper<ColorId>() {
            OwnerObject = wrapper.OwnerObject,
            PropertyInfo = wrapper.PropertyInfo
        })
        {

        }
    }
}
