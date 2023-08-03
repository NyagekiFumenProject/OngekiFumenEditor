using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public class PropertyInfoWrapper : PropertyChangedBase
    {
        public PropertyInfo PropertyInfo { get; set; }
        public virtual object OwnerObject { get; set; }

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

    public class MultiPropertyInfoWrapper : PropertyInfoWrapper
    {
        private PropertyInfoWrapper[] wrappers;

        public override object OwnerObject
        {
            get => new NotSupportedException();
            set => new NotSupportedException();
        }

        public MultiPropertyInfoWrapper(PropertyInfoWrapper[] wrappers)
        {
            this.wrappers = wrappers;
        }

        public override object ProxyValue
        {
            get
            {
                var itor = wrappers.Select(x => x.ProxyValue).GetEnumerator();
                if (!itor.MoveNext())
                    return default;
                var val = itor.Current;
                while (itor.MoveNext())
                {
                    if (val != itor.Current)
                        return default;
                }
                return val;
            }
            set
            {
                foreach (var wrapper in wrappers)
                    wrapper.ProxyValue = value;
            }
        }
    }
}
