using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Windows;

public partial class InternalTestWindowViewModel : WindowViewModelBase
{
    public InternalTestWindowViewModel()
    {
        
    }

    [ObservableProperty]
    public partial int CurrentValue { get; set; } = 50;
    
    [RelayCommand]
    private void CloseWindow()
    {
        TryCloseAsync(true);
    }
}