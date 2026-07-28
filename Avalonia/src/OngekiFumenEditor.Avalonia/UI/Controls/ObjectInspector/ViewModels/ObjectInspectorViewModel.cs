using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.ObjectModel;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class ObjectInspectorViewModel : ObservableObject
{
    [ObservableProperty]
    private object inspectObject;

    public ObservableCollection<IObjectPropertyAccessProxy> PropertyInfoWrappers { get; } = [];

    partial void OnInspectObjectChanged(object value)
    {
        OnObjectChanged();
    }

    private void OnObjectChanged()
    {
        PropertyInfoWrappers.Clear();

        var wrappers = (inspectObject?.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance) ?? Array.Empty<PropertyInfo>())
            .Where(x => x.CanRead)
            .Select(x => new PropertyInfoWrapper(x, inspectObject))
            .Where(x => x.PropertyInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() is null)
            .OrderBy(x => x.DisplayPropertyName)
            .ToArray();

        foreach (var wrapper in wrappers)
            PropertyInfoWrappers.Add(wrapper);
    }
}
