using Gekimini.Avalonia.Views;
using Avalonia;
using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector;

public partial class ObjectInspectorView : ViewBase
{
    public static readonly StyledProperty<object> InspectObjectProperty =
        AvaloniaProperty.Register<ObjectInspectorView, object>(nameof(InspectObject));

    public object InspectObject
    {
        get => GetValue(InspectObjectProperty);
        set => SetValue(InspectObjectProperty, value);
    }

    public ObjectInspectorView()
    {
        InitializeComponent();
        DataContext = new ObjectInspectorViewModel();
    }
}
