using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UI.Controls;

public partial class CommonOperationButton : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CommonOperationButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<IBrush> DecoratorBrushProperty =
        AvaloniaProperty.Register<CommonOperationButton, IBrush>(nameof(DecoratorBrush), Brushes.Black);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IBrush DecoratorBrush
    {
        get => GetValue(DecoratorBrushProperty);
        set => SetValue(DecoratorBrushProperty, value);
    }

    public CommonOperationButton()
    {
    }
}
