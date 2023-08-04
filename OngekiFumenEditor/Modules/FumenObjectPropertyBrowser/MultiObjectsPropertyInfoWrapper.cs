using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser
{
    public class MultiObjectsPropertyInfoWrapper : PropertyChangedBase, IObjectPropertyAccessProxy
    {
        private List<IObjectPropertyAccessProxy> wrappers;
        private PropertyInfo propertyInfo;

        public PropertyInfo PropertyInfo => propertyInfo;

        public string DisplayPropertyName => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAlias>()?.Alias ?? PropertyInfo.Name;
        public string DisplayPropertyTipText => string.Empty;

        private MultiObjectsPropertyInfoWrapper(List<IObjectPropertyAccessProxy> wrappers, PropertyInfo propertyInfo)
        {
            this.wrappers = wrappers;
            this.propertyInfo = propertyInfo;
        }

        public static bool TryCreate(string propertyName, Type propertyType, object[] objects, out MultiObjectsPropertyInfoWrapper multiWrapper)
        {
            //get real propInfo for every object.
            var list = new List<IObjectPropertyAccessProxy>();
            multiWrapper = default;

            foreach (var obj in objects)
            {
                var objType = obj.GetType();
                var propertyInfo = objType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo is null)
                {
                    Log.LogWarn($"object type {objType} does not contain property: {propertyName}({propertyType})");
                    continue;
                }
                if (propertyInfo.PropertyType != propertyType)
                {
                    Log.LogWarn($"object type {objType} property {propertyName} type not match: {propertyInfo.PropertyType} != {propertyType}");
                    continue;
                }

                var wrapper = new PropertyInfoWrapper(propertyInfo, obj);
                list.Add(wrapper);
            }

            if (list.Empty())
                return false;

            var propInfo = list.First().PropertyInfo;
            multiWrapper = new MultiObjectsPropertyInfoWrapper(list, propInfo);
            return true;
        }

        public void Dispose()
        {
            foreach (var wrapper in wrappers)
                wrapper.Dispose();
            wrappers = null;
        }

        public object DefaultValue
        {
            get
            {
                if (propertyInfo.PropertyType.IsValueType)
                    return Activator.CreateInstance(propertyInfo.PropertyType);
                return null;
            }
        }

        public object ProxyValue
        {
            get
            {
                //如果所有值都是一样的，那就返回正确的值，否则就返回default
                var itor = wrappers.GetEnumerator();
                if (!itor.MoveNext())
                    return DefaultValue;
                var val = itor.Current.ProxyValue;
                while (itor.MoveNext())
                {
                    if (val != itor.Current.ProxyValue)
                        return DefaultValue;
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
