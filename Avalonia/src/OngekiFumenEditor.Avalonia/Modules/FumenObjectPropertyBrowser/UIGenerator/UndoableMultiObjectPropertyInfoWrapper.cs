using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator;

public class UndoableMultiObjectPropertyInfoWrapper : ObservableObject, IObjectPropertyAccessProxy
{
    public PropertyInfo PropertyInfo => core.PropertyInfo;

    private readonly MultiObjectsPropertyInfoWrapper core;
    private readonly FumenVisualEditorViewModel referenceEditor;

    public bool IsReadOnly => core.IsReadOnly;
    public bool IsAllowSetNull => core.IsAllowSetNull;

    public UndoableMultiObjectPropertyInfoWrapper(MultiObjectsPropertyInfoWrapper propertyWrapperCore, FumenVisualEditorViewModel referenceEditor)
    {
        core = propertyWrapperCore;
        this.referenceEditor = referenceEditor;
        core.PropertyChanged += CorePropertyChanged;
    }

    private void CorePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue))
            OnPropertyChanged(nameof(ProxyValue));
        else
            OnPropertyChanged(e.PropertyName);
    }

    public object ProxyValue
    {
        get => core.ProxyValue;
        set
        {
            core.ProxyValue = value;
            OnPropertyChanged(nameof(ProxyValue));
        }
    }

    public string DisplayPropertyName => core.DisplayPropertyName;
    public string DisplayPropertyTipText => core.DisplayPropertyTipText;

    public void Clear()
    {
        core.PropertyChanged -= CorePropertyChanged;
        core.Clear();
    }
}
