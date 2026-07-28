using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;

public class MultiObjectsPropertyInfoWrapper : ObservableObject, IObjectPropertyAccessProxy
{
    private readonly List<IObjectPropertyAccessProxy> wrappers;
    private readonly PropertyInfo propertyInfo;
    private readonly IEqualityComparer comparer;

    public PropertyInfo PropertyInfo => propertyInfo;
    public IReadOnlyList<IObjectPropertyAccessProxy> Wrappers => wrappers;

    public string DisplayPropertyName => wrappers.First().DisplayPropertyName;
    public string DisplayPropertyTipText => wrappers.First().DisplayPropertyTipText;

    private static readonly Dictionary<Type, IEqualityComparer> CacheComparerMap = new();

    private MultiObjectsPropertyInfoWrapper(List<IObjectPropertyAccessProxy> wrappers, PropertyInfo propertyInfo)
    {
        this.wrappers = wrappers;
        this.propertyInfo = propertyInfo;

        if (!CacheComparerMap.TryGetValue(propertyInfo.PropertyType, out var cmp))
        {
            CacheComparerMap[propertyInfo.PropertyType] = cmp = typeof(EqualityComparer<>)
                .MakeGenericType(propertyInfo.PropertyType)
                .GetProperty(nameof(EqualityComparer<object>.Default))
                .GetValue(null) as IEqualityComparer;
        }

        foreach (var wrapper in wrappers)
            wrapper.PropertyChanged += WrapperPropertyChanged;

        comparer = cmp;
    }

    private void WrapperPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue))
            OnPropertyChanged(nameof(ProxyValue));
        else
            OnPropertyChanged(e.PropertyName);
    }

    public static bool TryCreate(string propertyName, Type propertyType, IEnumerable<object> objects, out MultiObjectsPropertyInfoWrapper multiWrapper)
    {
        var list = new List<IObjectPropertyAccessProxy>();
        multiWrapper = default;
        var isSingleSelected = objects.Count() == 1;

        foreach (var obj in objects)
        {
            var objType = obj.GetType();
            var propInfo = objType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propInfo is null)
            {
                Log.LogWarn($"object type {objType} does not contain property: {propertyName}({propertyType})");
                continue;
            }
            if (propInfo.PropertyType != propertyType)
            {
                Log.LogWarn($"object type {objType} property {propertyName} type not match: {propInfo.PropertyType} != {propertyType}");
                continue;
            }

            if (!propInfo.CanWrite && propInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() is null)
                continue;
            if (propInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() is not null)
                continue;
            if (propInfo.GetCustomAttribute<ObjectPropertyBrowserSingleSelectedOnly>() is not null && !isSingleSelected)
                continue;

            list.Add(new PropertyInfoWrapper(propInfo, obj));
        }

        if (list.Count == 0)
            return false;

        multiWrapper = new MultiObjectsPropertyInfoWrapper(list, list.First().PropertyInfo);
        return true;
    }

    public void Clear()
    {
        foreach (var wrapper in wrappers)
        {
            wrapper.PropertyChanged -= WrapperPropertyChanged;
            wrapper.Clear();
        }
    }

    public object DefaultValue => propertyInfo.PropertyType.IsValueType ? Activator.CreateInstance(propertyInfo.PropertyType) : null;

    public object ProxyValue
    {
        get
        {
            using var itor = wrappers.GetEnumerator();
            if (!itor.MoveNext())
                return AvaloniaProperty.UnsetValue;
            var val = itor.Current.ProxyValue;
            while (itor.MoveNext())
            {
                var cval = itor.Current.ProxyValue;
                if (!comparer.Equals(val, cval))
                    return AvaloniaProperty.UnsetValue;
            }
            return val;
        }
        set
        {
            if (ReferenceEquals(value, AvaloniaProperty.UnsetValue))
                return;
            foreach (var wrapper in wrappers)
                wrapper.ProxyValue = value;
        }
    }

    public bool IsAllowSetNull => wrappers.First().IsAllowSetNull;
    public bool IsReadOnly => wrappers.Any(x => x.IsReadOnly);
}
