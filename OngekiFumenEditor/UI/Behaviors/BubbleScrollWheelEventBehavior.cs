using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace OngekiFumenEditor.UI.Behaviors;

/// <summary>
/// Sends a mouse wheel event to the parent element.
/// </summary>
public class BubbleScrollWheelEventBehavior : Behavior<UIElement>
{
    protected override void OnAttached( )
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseWheel += AssociatedObject_PreviewMouseWheel ;
    }

    protected override void OnDetaching( )
    {
        AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
        base.OnDetaching();
    }

    void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {

        e.Handled = true;

        var e2 = new MouseWheelEventArgs(e.MouseDevice,e.Timestamp,e.Delta);
        e2.RoutedEvent = UIElement.MouseWheelEvent;
        AssociatedObject.RaiseEvent(e2);
    }
}