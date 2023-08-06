using Caliburn.Micro;
using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
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
        private IEqualityComparer comparer;

        public PropertyInfo PropertyInfo => propertyInfo;

        public string DisplayPropertyName => wrappers.First().DisplayPropertyName;
        public string DisplayPropertyTipText => wrappers.Count > 1 ? string.Empty : wrappers.First().DisplayPropertyTipText;

        private readonly Dictionary<Type, IEqualityComparer> cacheComparerMap = new();

        private MultiObjectsPropertyInfoWrapper(List<IObjectPropertyAccessProxy> wrappers, PropertyInfo propertyInfo)
        {
            this.wrappers = wrappers;
            this.propertyInfo = propertyInfo;

            if (!cacheComparerMap.TryGetValue(propertyInfo.PropertyType, out var cmp))
            {
                cacheComparerMap[propertyInfo.PropertyType] = cmp = typeof(EqualityComparer<>)
                    .MakeGenericType(propertyInfo.PropertyType)
                    .GetProperty(nameof(EqualityComparer<object>.Default))
                    .GetValue(null) as IEqualityComparer;
            }

            comparer = cmp;
        }

        public static bool TryCreate(string propertyName, Type propertyType, IEnumerable<object> objects, out MultiObjectsPropertyInfoWrapper multiWrapper)
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

                if (!propertyInfo.CanWrite)
                {
                    if (propertyInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() == null)
                        continue;
                }
                if (propertyInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() != null)
                    continue;

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

        /// <summary>
        /// 可能返回DependencyProperty.UnsetValue表示不同的值
        /// </summary>
        public object ProxyValue
        {
            get
            {
                //如果所有值都是一样的，那就返回正确的值，否则就返回default
                var itor = wrappers.GetEnumerator();
                if (!itor.MoveNext())
                    return DependencyProperty.UnsetValue;
                var val = itor.Current.ProxyValue;
                while (itor.MoveNext())
                {
                    var cval = itor.Current.ProxyValue;
                    if (!comparer.Equals(val, cval))
                        return DependencyProperty.UnsetValue;
                }
                return val;
            }
            set
            {
                if (value == DependencyProperty.UnsetValue)
                    return;
                foreach (var wrapper in wrappers)
                    wrapper.ProxyValue = value;
            }
        }
    }
}
