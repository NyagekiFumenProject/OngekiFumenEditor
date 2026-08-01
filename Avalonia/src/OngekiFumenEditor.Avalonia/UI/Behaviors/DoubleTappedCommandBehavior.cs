using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace OngekiFumenEditor.Avalonia.UI.Behaviors;

public sealed class DoubleTappedCommandBehavior : Behavior<InputElement>
{
    public static readonly StyledProperty<ICommand> CommandProperty =
        AvaloniaProperty.Register<DoubleTappedCommandBehavior, ICommand>(nameof(Command));

    public static readonly StyledProperty<object> CommandParameterProperty =
        AvaloniaProperty.Register<DoubleTappedCommandBehavior, object>(nameof(CommandParameter));

    public ICommand Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        if (AssociatedObject is not null)
            AssociatedObject.DoubleTapped += OnDoubleTapped;
    }

    protected override void OnDetachedFromVisualTree()
    {
        if (AssociatedObject is not null)
            AssociatedObject.DoubleTapped -= OnDoubleTapped;
        base.OnDetachedFromVisualTree();
    }

    private void OnDoubleTapped(object sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
}
