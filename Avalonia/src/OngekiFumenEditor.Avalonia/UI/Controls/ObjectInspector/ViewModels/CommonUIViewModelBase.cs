using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public abstract class CommonUIViewModelBase : ObservableObject
{
    private IObjectPropertyAccessProxy propertyInfo;

    protected CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper)
    {
        PropertyInfo = wrapper;
        PropertyInfo.PropertyChanged += PropertyInfoPropertyChanged;
    }

    protected virtual void PropertyInfoPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == propertyInfo.PropertyInfo.Name)
            OnPropertyChanged(e.PropertyName);
    }

    public IObjectPropertyAccessProxy PropertyInfo
    {
        get => propertyInfo;
        set => SetProperty(ref propertyInfo, value);
    }
}

public abstract class CommonUIViewModelBase<T> : CommonUIViewModelBase where T : class
{
    protected CommonUIViewModelBase(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    public T TypedProxyValue
    {
        get => ProxyValue as T;
        set => ProxyValue = value;
    }

    public object ProxyValue
    {
        get => PropertyInfo.ProxyValue;
        set => PropertyInfo.ProxyValue = value;
    }

    protected override void PropertyInfoPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProxyValue):
                Refresh();
                break;
            default:
                base.PropertyInfoPropertyChanged(sender, e);
                break;
        }
    }

    protected virtual void Refresh()
    {
    }
}
