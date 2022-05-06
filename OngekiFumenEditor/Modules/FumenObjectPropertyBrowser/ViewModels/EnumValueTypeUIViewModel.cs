using Caliburn.Micro;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class EnumValueTypeUIViewModel : CommonUIViewModelBase
    {
        public IEnumerable<string> EnumValues => Enum.GetNames(PropertyInfo.PropertyInfo.PropertyType);

        public string Value
        {
            get => PropertyInfo.ProxyValue?.ToString();
            set => PropertyInfo.ProxyValue = Enum.Parse(PropertyInfo.PropertyInfo.PropertyType, value);
        }

        public EnumValueTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {

        }
    }
}
