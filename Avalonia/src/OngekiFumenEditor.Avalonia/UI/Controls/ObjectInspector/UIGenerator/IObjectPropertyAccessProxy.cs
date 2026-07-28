using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;

public interface IObjectPropertyAccessProxy
{
    PropertyInfo PropertyInfo { get; }
    object ProxyValue { get; set; }

    string DisplayPropertyName { get; }
    string DisplayPropertyTipText { get; }

    bool IsAllowSetNull { get; }
    bool IsReadOnly { get; }

    void Clear();
}
