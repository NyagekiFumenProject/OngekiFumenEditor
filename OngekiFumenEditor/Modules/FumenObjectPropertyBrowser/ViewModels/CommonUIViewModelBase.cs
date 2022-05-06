using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public abstract class CommonUIViewModelBase : PropertyChangedBase
    {
        private PropertyInfoWrapper propertyInfo;
        private readonly string propName;

        public CommonUIViewModelBase(PropertyInfoWrapper wrapper)
        {
            PropertyInfo = wrapper;
            propName = wrapper.PropertyInfo.Name;
            if (wrapper.OwnerObject is PropertyChangedBase changable)
                changable.PropertyChanged += Wrapper_PropertyChanged;
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
                NotifyOfPropertyChange(() => PropertyInfo);
            }
        }

        private void Wrapper_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != propName)
                return;

            NotifyOfPropertyChange(() => PropertyInfo);
        }
    }

    public abstract class CommonUIViewModelBase<T> : CommonUIViewModelBase where T : class
    {
        public T TypedProxyValue
        {
            get => PropertyInfo.ProxyValue as T;
            set => PropertyInfo.ProxyValue = value;
        }

        protected CommonUIViewModelBase(PropertyInfoWrapper wrapper) : base(wrapper)
        {
        }
    }
}
