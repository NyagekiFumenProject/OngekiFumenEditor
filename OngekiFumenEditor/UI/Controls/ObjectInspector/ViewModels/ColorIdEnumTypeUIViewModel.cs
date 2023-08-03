using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class ColorIdEnumTypeUIViewModel : CommonUIViewModelBase
    {
        public IEnumerable<ColorId> EnumValues => ColorIdConst.AllColors;

        public ColorIdEnumTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {

        }
    }
}
