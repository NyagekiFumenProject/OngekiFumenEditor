using Avalonia.Controls;
using Gekimini.Avalonia.Modules.Window.Views;
using OngekiFumenEditor.Avalonia.UI.Dialogs.ViewModels;

namespace OngekiFumenEditor.Avalonia.UI.Dialogs.Views;

public partial class CommonColorPickerView : WindowViewBase
{
    public CommonColorPickerView()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!e.Cancel && DataContext is CommonColorPickerViewModel viewModel)
            viewModel.CancelChanges();
    }
}
