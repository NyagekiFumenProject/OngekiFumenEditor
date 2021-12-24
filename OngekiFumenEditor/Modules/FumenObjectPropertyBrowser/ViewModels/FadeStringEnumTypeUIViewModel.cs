using Caliburn.Micro;
using OngekiFumenEditor.Base;
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
    public class FadeStringEnumTypeUIViewModel : CommonUIViewModelBase
    {
        public IEnumerable<FadeStringEnum> EnumValues => PropertyInfo.PropertyInfo.PropertyType.GetProperties(BindingFlags.Static | BindingFlags.Public)
                    .Where(x => x.CanRead && !x.CanWrite)
                    .Select(x => x.GetValue(null))
                    .OfType<FadeStringEnum>();

        public FadeStringEnumTypeUIViewModel(PropertyInfoWrapper wrapper) : base(new PropertyInfoWrapper<FadeStringEnum>() {
            OwnerObject = wrapper.OwnerObject,
            PropertyInfo = wrapper.PropertyInfo
        })
        {

        }
    }
}
