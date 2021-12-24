using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator
{
    public class PropertyInfoWrapper : PropertyChangedBase
    {
        public PropertyInfo PropertyInfo { get; set; }
        public object OwnerObject { get; set; }

        public virtual object ProxyValue
        {
            get
            {
                return PropertyInfo.GetValue(OwnerObject);
            }
            set
            {
                var actualType = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                PropertyInfo.SetValue(OwnerObject, actualType.ConvertFrom(value));
                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public string DisplayPropertyName => PropertyInfo.Name;

        public override string ToString() => $"PropName:{DisplayPropertyName} PropValue:{ProxyValue}";
    }

    public class PropertyInfoWrapper<T> : PropertyInfoWrapper
    {
        public new T ProxyValue
        {
            get
            {
                return (T)PropertyInfo.GetValue(OwnerObject);
            }
            set
            {
                PropertyInfo.SetValue(OwnerObject, value);
                NotifyOfPropertyChange(() => ProxyValue);
            }
        }
    }
}
