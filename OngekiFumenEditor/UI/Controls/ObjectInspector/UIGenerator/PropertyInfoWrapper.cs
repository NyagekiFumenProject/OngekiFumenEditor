using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
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
                var valType = value?.GetType() ?? default;
                if (PropertyInfo.PropertyType == valType || valType is null || valType.IsAssignableTo(PropertyInfo.PropertyType))
                {
                    PropertyInfo.SetValue(OwnerObject, value);
                }
                else
                {
                    var actualType = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                    PropertyInfo.SetValue(OwnerObject, actualType.ConvertFrom(value));
                }

                NotifyOfPropertyChange(() => ProxyValue);
            }
        }

        public string DisplayPropertyName => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAlias>()?.Alias ?? PropertyInfo.Name;
        public string DisplayPropertyTipText => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserTipText>()?.TipText ?? string.Empty;

        public override string ToString() => $"DisplayName:{DisplayPropertyName} PropValue:{ProxyValue}";
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
