using Caliburn.Micro;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public abstract class CommonUIViewModelBase : PropertyChangedBase
    {
        private IObjectPropertyAccessProxy propertyInfo;
        private readonly string propName;

        public CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper)
        {
            PropertyInfo = wrapper;
            propName = wrapper.PropertyInfo.Name;
        }

        public IObjectPropertyAccessProxy PropertyInfo
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
    }

    public abstract class CommonUIViewModelBase<T> : CommonUIViewModelBase where T : class
    {
        public T TypedProxyValue
        {
            get => PropertyInfo.ProxyValue as T;
            set => PropertyInfo.ProxyValue = value;
        }

        protected CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }
    }
}
