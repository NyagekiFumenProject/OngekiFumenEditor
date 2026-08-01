using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.ComponentModel;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs;

public partial class CommonColorPicker : Window, INotifyPropertyChanged
{
    private readonly Func<Color> getter;
    private readonly Action<Color> setter;

    public new event PropertyChangedEventHandler PropertyChanged;

    public Color CurrentColor
    {
        get => getter?.Invoke() ?? Colors.White;
        set
        {
            setter?.Invoke(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentColor)));
        }
    }

    public CommonColorPicker(Func<Color> getter, Action<Color> setter, string title)
    {
        this.getter = getter;
        this.setter = setter;
        InitializeComponent();
        DataContext = this;
        Title = title;
    }

    public CommonColorPicker()
    {
        getter = () => Colors.White;
        setter = _ => { };
        InitializeComponent();
        DataContext = this;
    }

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Background: SolidColorBrush brush })
            CurrentColor = brush.Color;
    }
}
