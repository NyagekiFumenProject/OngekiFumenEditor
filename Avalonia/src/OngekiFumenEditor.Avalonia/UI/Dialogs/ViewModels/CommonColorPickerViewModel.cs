using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

public partial class CommonColorPickerViewModel : WindowViewModelBase
{
    private readonly Action<Color> setter;
    private readonly Color initialColor;
    private Color currentColor;
    private bool isAccepted;

    public string Title { get; }

    public Color CurrentColor
    {
        get => currentColor;
        set
        {
            if (currentColor == value)
                return;

            currentColor = value;
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
        ArgumentNullException.ThrowIfNull(getter);
        this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
        initialColor = currentColor = getter();
        Title = string.IsNullOrWhiteSpace(title) ? "CommonColorPicker" : title;
    }

    [RelayCommand]
    private void SelectColor(string colorText)
    {
        Log.LogInfo($"SelectColor triggered with {colorText}.");
        if (Color.TryParse(colorText, out var color))
            CurrentColor = color;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        Log.LogInfo($"Color picker confirmed (title='{Title}', color='{currentColor}').");
        isAccepted = true;
        await TryCloseAsync(true);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        Log.LogInfo($"Color picker cancelled (title='{Title}').");
        CancelChanges();
        await TryCloseAsync(false);
    }

    internal void CancelChanges()
    {
        if (isAccepted)
            return;

        CurrentColor = initialColor;
    }
}
