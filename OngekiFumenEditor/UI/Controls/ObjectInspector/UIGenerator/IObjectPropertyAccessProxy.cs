using System;
using System.ComponentModel;
using System.Reflection;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator
{
    public interface IObjectPropertyAccessProxy : INotifyPropertyChanged, IDisposable
    {
        PropertyInfo PropertyInfo { get; }
        object ProxyValue { get; set; }

        string DisplayPropertyName { get; }
        string DisplayPropertyTipText { get; }
    }
}
