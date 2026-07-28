using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

public class PropertyInfoWrapper : ObservableObject, IObjectPropertyAccessProxy
{
    public PropertyInfo PropertyInfo { get; private set; }
    private object ownerObject;

    public PropertyInfoWrapper(PropertyInfo propertyInfo, object owner)
    {
        PropertyInfo = propertyInfo;
        ownerObject = owner;

        if (ProxyValue is INotifyPropertyChanged np)
            np.PropertyChanged += OpPropertyChanged;
        if (ownerObject is INotifyPropertyChanged onp)
            onp.PropertyChanged += OnOwnerPropertyChanged;
    }

    public virtual object ProxyValue
    {
        get
        {
#if DEBUG
            if (ownerObject is null)
                throw new ObjectDisposedException(nameof(PropertyInfoWrapper));
#endif
            return PropertyInfo.GetValue(ownerObject);
        }
        set
        {
#if DEBUG
            if (ownerObject is null)
                throw new ObjectDisposedException(nameof(PropertyInfoWrapper));
#endif
            var valType = value?.GetType();
            if (PropertyInfo.PropertyType == valType || valType is null || valType.IsAssignableTo(PropertyInfo.PropertyType))
            {
                SetValueInternal(value);
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(PropertyInfo.PropertyType);
                var actualValue = converter.ConvertFrom(value);
                SetValueInternal(actualValue);
            }

            OnPropertyChanged(nameof(ProxyValue));
        }
    }

    private void SetValueInternal(object newValue)
    {
        var oldValue = ProxyValue;
        if (oldValue is INotifyPropertyChanged op)
            op.PropertyChanged -= OpPropertyChanged;
        if (newValue is INotifyPropertyChanged np)
            np.PropertyChanged += OpPropertyChanged;

        PropertyInfo.SetValue(ownerObject, newValue);
    }

    private void OpPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ProxyValue));
    }

    private void OnOwnerPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == PropertyInfo.Name)
        {
            OnPropertyChanged(nameof(ProxyValue));
            OnPropertyChanged(e.PropertyName);
        }
    }

    public string DisplayPropertyName => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAlias>()?.Alias ?? PropertyInfo.Name;

    public string DisplayPropertyTipText => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserTipText>()?.TipText ?? string.Empty;

    public bool IsAllowSetNull => PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserAllowSetNull>() is not null;

    public bool IsReadOnly
    {
        get
        {
            var editable = PropertyInfo.CanWrite && PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserReadOnly>() is null;
            if (editable && PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserReadOnlyForCondition>() is { } condition)
                editable = !condition.CheckIfReadOnly(ownerObject);

            return !editable;
        }
    }

    public override string ToString() => $"DisplayName:{DisplayPropertyName} PropValue:{ProxyValue}";

    public void Clear()
    {
        if (ProxyValue is INotifyPropertyChanged np)
            np.PropertyChanged -= OpPropertyChanged;
        if (ownerObject is INotifyPropertyChanged onp)
            onp.PropertyChanged -= OnOwnerPropertyChanged;
    }
}
