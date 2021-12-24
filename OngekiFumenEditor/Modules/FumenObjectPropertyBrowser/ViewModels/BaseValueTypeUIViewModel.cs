using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class BaseValueTypeUIViewModel : PropertyChangedBase
    {
        private PropertyInfoWrapper propertyInfo;

        public BaseValueTypeUIViewModel(PropertyInfoWrapper wrapper)
        {
            PropertyInfo = wrapper;
        }

        public PropertyInfoWrapper PropertyInfo
        {
            get
            {
                return propertyInfo;
            }
            set
            {
                propertyInfo = value;
                this.NotifyOfPropertyChange(() => PropertyInfo);
            }
        }
    }
}
