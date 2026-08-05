using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

public partial class CommonColorPickerViewModel : WindowViewModelBase
{
    private readonly Func<Color> getter;
    private readonly Action<Color> setter;

    public string Title { get; }

    public Color CurrentColor
    {
        get => getter();
        set
        {
            setter(value);
            OnPropertyChanged();
        }
    }

    public CommonColorPickerViewModel()
        : this(() => Colors.White, static _ => { }, "CommonColorPicker")
    {
    }

    public CommonColorPickerViewModel(Func<Color> getter, Action<Color> setter, string title)
    {
        this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
        this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
        Title = string.IsNullOrWhiteSpace(title) ? "CommonColorPicker" : title;
    }

    [RelayCommand]
    private void SelectColor(string colorText)
    {
        if (string.IsNullOrWhiteSpace(colorText))
            return;

        CurrentColor = Color.Parse(colorText);
    }
}
